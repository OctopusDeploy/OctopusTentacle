using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Octopus.Tentacle.Security.Masking;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Security
{
    [TestFixture]
    public class SensitiveDataMaskFixture
    {
        [Test]
        public void LongestValuesAreMasked()
        {
            const string sensitive = "sensitive",
                verysensitive = "sensitiveHELLO",
                prettysensitive = "veHELLO",
                raw = "This contains a sensitiveHELLO value",
                expected = "This contains a ******** value";

            var sdm = new SensitiveDataMask();
            var trie = CreateTrie(verysensitive, sensitive, prettysensitive);
            var result = default(string);
            sdm.ApplyTo(trie,
                raw,
                r => result = r
            );
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void ASensitiveValueIsMasked()
        {
            const string sensitive = "sensitive",
                raw = "This contains a sensitive value",
                expected = "This contains a ******** value";

            var sdm = new SensitiveDataMask();
            var trie = CreateTrie(sensitive);
            string result = null;
            sdm.ApplyTo(trie, raw, sanitized => result = sanitized);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void ARegularValueIsNotMasked()
        {
            const string sensitive = "sensitive",
                raw = "This contains no such value";

            var sdm = new SensitiveDataMask();
            var trie = CreateTrie(sensitive);
            string result = null;
            sdm.ApplyTo(trie, raw, sanitized => result = sanitized);

            Assert.AreSame(raw, result);
        }

        [Test]
        public void MultipleSensitiveValueAreMasked()
        {
            const string sensitive = "sensitive",
                raw = "This contains a sensitive value in a sensitive place at a sensitive time",
                expected = "This contains a ******** value in a ******** place at a ******** time";

            var sdm = new SensitiveDataMask();
            var trie = CreateTrie(sensitive);
            string result = null;
            sdm.ApplyTo(trie, raw, sanitized => result = sanitized);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void ShouldMaskMultipleMatches()
        {
            var sdm = new SensitiveDataMask();
            var trie = CreateTrie("bacon", "eggs");
            string result = null;
            sdm.ApplyTo(trie, "I love bacon and eggs!", sanitized => result = sanitized);

            Assert.AreEqual("I love ******** and ********!", result);
        }

        [Test]
        public void ShouldMaskOverlappingMatches()
        {
            var sdm = new SensitiveDataMask();
            var trie = CreateTrie("meenie mi", "nie minie");
            string result = null;
            sdm.ApplyTo(trie, "eenie meenie minie mo", sanitized => result = sanitized);

            Assert.AreEqual("eenie ******** mo", result);
        }

        [Test]
        public void AValueShorterThan4CharactersIsNotMasked()
        {
            const string sensitive = "123",
                raw = "Easy as 123";

            var sdm = new SensitiveDataMask();
            var trie = CreateTrie(sensitive);

            string result = null;
            sdm.ApplyTo(trie, raw, sanitized => result = sanitized);

            Assert.AreSame(raw, result);
        }

        [Test]
        [Ignore("Development time testing only")]
        public void HeavyLoadPerformance()
        {
            var sensitiveValues = new string[1000];
            var rng = new Random();
            var watch = new Stopwatch();

            for (var i = 0; i < sensitiveValues.Length; i++)
                sensitiveValues[i] = RandomStringGenerator.Generate(rng.Next(8, 50));

            watch.Start();
            var sdm = new SensitiveDataMask();
            var trie = CreateTrie(sensitiveValues);
            watch.Stop();
            var preProcessingTime = watch.ElapsedMilliseconds;

            watch.Restart();
            for (var i = 0; i < 100000; i++)
            {
                var raw = RandomStringGenerator.Generate(rng.Next(30, 500));

                if (i % 100 == 0)
                    raw += sensitiveValues[rng.Next(0, 999)];

                sdm.ApplyTo(trie, raw, sensitive => { });
            }

            watch.Stop();
            var processingTime = watch.ElapsedMilliseconds;

            Console.WriteLine($"Pre-Processing time: {preProcessingTime} milliseconds");
            Console.WriteLine($"Processing time: {processingTime} milliseconds");

            Assert.That(preProcessingTime, Is.LessThan(250));
            Assert.That(preProcessingTime, Is.LessThan(60000));
        }

        [Test]
        [Ignore("Development time testing only")]
        public void PerformanceOnMatches()
        {
            var password = Guid.NewGuid().ToString();
            var line = string.Concat(Enumerable.Range(0, 10).Select(g => Guid.NewGuid().ToString()))
                + password
                + string.Concat(Enumerable.Range(0, 10).Select(g => Guid.NewGuid().ToString()));

            var sdm = new SensitiveDataMask();
            var trie = CreateTrie(password);

            var watch = Stopwatch.StartNew();
            var count = 0;
            while (watch.ElapsedMilliseconds < 5000)
            {
                sdm.ApplyTo(trie, line, sanitized => { });
                count++;
            }

            Console.WriteLine(count.ToString("n0"));
            Assert.That(count, Is.GreaterThan(1000));
        }

        [Test]
        [Ignore("Development time testing only")]
        public void PerformanceOnNonMatches()
        {
            var line = string.Concat(Enumerable.Range(0, 21).Select(g => Guid.NewGuid().ToString()));

            var sdm = new SensitiveDataMask();
            var trie = CreateTrie(Guid.NewGuid().ToString());

            var watch = Stopwatch.StartNew();
            var count = 0;
            while (watch.ElapsedMilliseconds < 5000)
            {
                sdm.ApplyTo(trie, line, sanitized => { });
                count++;
            }

            Console.WriteLine(count.ToString("n0"));
            Assert.That(count, Is.GreaterThan(1000));
        }

        AhoCorasick CreateTrie(params string[] args)
        {
            var trie = new AhoCorasick();
            foreach (var instance in args)
            {
                if (string.IsNullOrWhiteSpace(instance) || instance.Length < 4)
                    continue;

                var normalized = instance.Replace("\r\n", "").Replace("\n", "");

                trie.Add(normalized);
            }

            trie.Build();
            return trie;
        }
    }
}