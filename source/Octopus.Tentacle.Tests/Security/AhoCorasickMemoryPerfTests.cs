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
            var before = GetLiveHeapBytes();

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

            var after = GetLiveHeapBytes();

            // The trie is nothing but nodes: these inputs produce 399,823 of them, and a node is 5 references
            // plus 2 chars, so 32 bytes on 32-bit (~12MiB) and 64 bytes on 64-bit (~24MiB). Fail if we exceed
            // 15MiB / 30MiB, i.e. if a node grows by a field or the trie stops sharing prefixes.
            var allowedMb = Environment.Is64BitProcess ? 30 : 15;

            var usedBytes = after - before;
            Console.WriteLine($"Used {usedBytes / 1024.0 / 1024.0:F1}MB, allowing up to {allowedMb}MB");
            Assert.That(usedBytes, Is.LessThan(allowedMb * 1024 * 1024));

            GC.KeepAlive(trie);
        }

        /// <summary>
        /// The size of the live managed heap, which is what this test wants to compare before and after.
        /// </summary>
        /// <remarks>
        /// Deliberately not GC.GetTotalMemory: since .NET 7 that over-reports on the "segments" GC by counting
        /// whatever part of gen1/gen2 shares the ephemeral segment twice - once in the gen0 span it measures from
        /// the start of that segment, and again when it adds the generation sizes (dotnet/runtime#134755). How much
        /// of gen2 sits in the ephemeral segment depends on the heap's history, so the over-report is not even a
        /// constant factor: forcing segments on 64-bit Linux, the same trie measured anywhere between 37MiB and
        /// 50MiB against a true 24MiB depending only on what was allocated beforehand. Segments are used wherever
        /// regions are not, which is every 32-bit process (regions need HOST_64BIT) and macOS - so win-x86 saw this
        /// test fail intermittently at ~15-16MiB against the 15MiB budget while the real footprint never moved.
        /// Remove this once we are on a runtime with #134755 fixed, or once nothing we test on uses segments.
        /// </remarks>
        static long GetLiveHeapBytes()
        {
#if NETFRAMEWORK
            // .NET Framework predates the regression above (and GC.GetGCMemoryInfo).
            return GC.GetTotalMemory(true);
#else
            // Twice, so finalized objects are collected too, and compacting so HeapSizeBytes is not inflated by free space.
            for (var i = 0; i < 2; i++)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
            }

            var info = GC.GetGCMemoryInfo();
            return info.HeapSizeBytes - info.FragmentedBytes;
#endif
        }
    }
}
