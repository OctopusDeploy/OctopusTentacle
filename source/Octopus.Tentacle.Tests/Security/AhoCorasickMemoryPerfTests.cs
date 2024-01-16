using System;
using System.Linq;
using NUnit.Framework;
using Octopus.Tentacle.Diagnostics.Masking;

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

            // Currently this code uses ~12Mb of memory. Fail if it exceeds 15Mb (on 32bit).
            var allowedMb = Environment.Is64BitProcess ? 30 : 15;
            Console.WriteLine($"Allowing up to {allowedMb}MB");
            Assert.That(after - before, Is.LessThan(allowedMb * 1024 * 1024));
        }
    }
}