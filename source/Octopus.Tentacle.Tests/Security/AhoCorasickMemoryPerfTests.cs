using System;
using System.Linq;
using NUnit.Framework;
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
            var before = MeasureLiveHeapBytes();

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

            var after = MeasureLiveHeapBytes();
            GC.KeepAlive(trie);

            // On 64-bit this measures ~24MiB. Fail if it exceeds 30MiB (15MiB on 32bit).
            var allowedMb = Environment.Is64BitProcess ? 30 : 15;

            var usedBytes = after - before;
            Console.WriteLine($"Used {usedBytes / 1024.0 / 1024.0:F1}MB, allowing up to {allowedMb}MB");
            Assert.That(usedBytes, Is.LessThan(allowedMb * 1024 * 1024));
        }

        // Live bytes on the managed heap after a full GC.
        // Not GC.GetTotalMemory: on the older "segments" GC (32-bit processes, e.g. win-x86, and macOS) it counts gen1/gen2 twice (dotnet/runtime#134755),
        // so it over-reports by an amount that depends on whatever else is on the heap. On win-x86 that put the reading anywhere from ~11MiB to ~16MiB.
        static long MeasureLiveHeapBytes()
        {
#if NETFRAMEWORK
            return GC.GetTotalMemory(true);
#else
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var info = GC.GetGCMemoryInfo();
            return info.HeapSizeBytes - info.FragmentedBytes;
#endif
        }
    }
}