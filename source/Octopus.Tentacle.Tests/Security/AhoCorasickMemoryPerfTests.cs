using System;
using System.Linq;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Core.Services.Scripts.Security.Masking;

namespace Octopus.Tentacle.Tests.Security
{
    [TestFixture]
    public class AhoCorasickMemoryPerfTests
    {
        const int wordCount = 200;
        const int wordLength = 2000;
        const int searchCount = 20;

        readonly string[] words;
        readonly Random random;
        readonly string[] toSearch;
        readonly bool[] result;

        public AhoCorasickMemoryPerfTests()
        {
            random = new Random(42);

            words = Enumerable.Range(0, wordCount)
                .Select(_ => RandomString(wordLength))
                .ToArray();

            result = Enumerable.Range(0, searchCount)
                .Select(_ => random.Next() % 2 == 0)
                .ToArray();

            toSearch = Enumerable.Range(0, result.Length)
                .Select(i => result[i] ? RandomString(wordLength / 2) + words[random.Next(wordCount)] : RandomString(wordLength * 2))
                .ToArray();
        }

        string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)])
                .ToArray());
        }

        [Test]
        public void EnsureTreeMemorySizeRemainsSmall()
        {
            var before = GC.GetTotalMemory(true);

            var trie = new AhoCorasick();
            for (var i = 0; i < words.Length; i++)
                trie.Add(words[i]);
            trie.Build();

            for (var i = 0; i < toSearch.Length; i++)
            {
                var found = trie.Find(toSearch[i]);
                if (!found.IsPartial && found.Found.Any() != result[i])
                    throw new Exception("Find mistake");
            }

            var after = GC.GetTotalMemory(true);

            // On 64-bit Linux this measures ~24MiB. Fail if it exceeds 30MiB (15MiB on 32bit).
            //
            // On macOS GC.GetTotalMemory reports ~2x for the same heap (net8.0: ~50MiB on
            // arm64 and ~49MiB on x64, vs ~24MiB on Linux on the same hardware, while
            // GC.GetGCMemoryInfo().HeapSizeBytes matches), so allow double there.
            var allowedMb = Environment.Is64BitProcess ? 30 : 15;
            if (PlatformDetection.IsRunningOnMac)
                allowedMb *= 2;

            var usedBytes = after - before;
            Console.WriteLine($"Used {usedBytes / 1024.0 / 1024.0:F1}MB, allowing up to {allowedMb}MB");
            Assert.That(usedBytes, Is.LessThan(allowedMb * 1024 * 1024));
        }
    }
}