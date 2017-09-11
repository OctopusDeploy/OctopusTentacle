using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Octopus.Shared.Security.Masking;
using Octopus.Shared.Util;

namespace Octopus.Shared.Tests.Security
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
            sdm.MaskInstancesOf(verysensitive);
            sdm.MaskInstancesOf(sensitive);
            sdm.MaskInstancesOf(prettysensitive);
            sdm.ApplyTo(raw, result =>
            {
                Assert.AreEqual(expected, result);
            });
        }

        [Test]
        public void ASensitiveValueIsMasked()
        {
            const string sensitive = "sensitive",
                raw = "This contains a sensitive value",
                expected = "This contains a ******** value";

            var sdm = new SensitiveDataMask();
            sdm.MaskInstancesOf(sensitive);
            string result = null;
            sdm.ApplyTo(raw, sanitized => result = sanitized);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void ARegularValueIsNotMasked()
        {
            const string sensitive = "sensitive",
                raw = "This contains no such value";

            var sdm = new SensitiveDataMask();
            sdm.MaskInstancesOf(sensitive);
            string result = null;
            sdm.ApplyTo(raw, sanitized => result = sanitized);

            Assert.AreSame(raw, result);
        }

        [Test]
        public void MultipleSensitiveValueAreMasked()
        {
            const string sensitive = "sensitive",
                raw = "This contains a sensitive value in a sensitive place at a sensitive time",
                expected = "This contains a ******** value in a ******** place at a ******** time";

            var sdm = new SensitiveDataMask();
            sdm.MaskInstancesOf(sensitive);
            string result = null;
            sdm.ApplyTo(raw, sanitized => result = sanitized);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void ShouldMaskMultipleMatches()
        {
            var sdm = new SensitiveDataMask();
            sdm.MaskInstancesOf("bacon");
            sdm.MaskInstancesOf("eggs");
            string result = null;
            sdm.ApplyTo("I love bacon and eggs!", sanitized => result = sanitized);

            Assert.AreEqual("I love ******** and ********!", result);
        }

        [Test]
        public void ShouldMaskOverlappingMatches()
        {
            var sdm = new SensitiveDataMask();
            sdm.MaskInstancesOf("meenie mi");
            sdm.MaskInstancesOf("nie minie");
            string result = null;
            sdm.ApplyTo("eenie meenie minie mo", sanitized => result = sanitized);

            Assert.AreEqual("eenie ******** mo", result);
        }

        [Test]
        public void AValueShorterThan4CharactersIsNotMasked()
        {
            const string sensitive = "123",
                raw = "Easy as 123";

            var sdm = new SensitiveDataMask();
            sdm.MaskInstancesOf(sensitive);

            string result = null;
            sdm.ApplyTo(raw, sanitized => result = sanitized);

            Assert.AreSame(raw, result);
        }

        [Test]
        [Ignore]
        public void HeavyLoadPerformance()
        {
            var sensitiveValues = new List<string>();
            var rng = new Random();
            var watch = new Stopwatch();

            for (var i = 0; i < 1000; i++)
            {
                sensitiveValues.Add(RandomStringGenerator.Generate(rng.Next(8, 50)));
            }

            watch.Start();
            var sdm = new SensitiveDataMask();
            sdm.MaskInstancesOf(sensitiveValues);
            watch.Stop();
            var preProcessingTime = watch.ElapsedMilliseconds;

            watch.Restart();
            for (var i = 0; i < 100000; i++)
            {
                var raw = RandomStringGenerator.Generate(rng.Next(30, 500));

                if (i % 100 == 0)
                    raw += sensitiveValues[rng.Next(0, 999)];

                sdm.ApplyTo(raw, sensitive => { });
            }
            watch.Stop();
            var processingTime = watch.ElapsedMilliseconds;

            Console.WriteLine($"Pre-Processing time: {preProcessingTime} milliseconds");
            Console.WriteLine($"Processing time: {processingTime} milliseconds");

            Assert.That(preProcessingTime, Is.LessThan(250));
            Assert.That(preProcessingTime, Is.LessThan(60000));
        }

        [Test]
        [Ignore]
        public void PerformanceOnMatches()
        {
            var password = Guid.NewGuid().ToString();
            var line = string.Concat(Enumerable.Range(0, 10).Select(g => Guid.NewGuid().ToString()))
                + password
                + string.Concat(Enumerable.Range(0, 10).Select(g => Guid.NewGuid().ToString()));

            var sdm = new SensitiveDataMask();
            sdm.MaskInstancesOf(password);

            var watch = Stopwatch.StartNew();
            var count = 0;
            while (watch.ElapsedMilliseconds < 5000)
            {
                sdm.ApplyTo(line, sanitized => { });
                count++;
            }

            Console.WriteLine(count.ToString("n0"));
            Assert.That(count, Is.GreaterThan(1000));
        }

        [Test]
        [Ignore]
        public void PerformanceOnNonMatches()
        {
            var line = string.Concat(Enumerable.Range(0, 21).Select(g => Guid.NewGuid().ToString()));

            var sdm = new SensitiveDataMask();
            sdm.MaskInstancesOf(Guid.NewGuid().ToString());

            var watch = Stopwatch.StartNew();
            var count = 0;
            while (watch.ElapsedMilliseconds < 5000)
            {
                sdm.ApplyTo(line, sanitized => { });
                count++;
            }

            Console.WriteLine(count.ToString("n0"));
            Assert.That(count, Is.GreaterThan(1000));
        }
    }
}