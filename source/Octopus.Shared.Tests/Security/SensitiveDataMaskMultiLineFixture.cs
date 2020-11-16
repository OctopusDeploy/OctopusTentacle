using System;
using NUnit.Framework;
using Octopus.Shared.Security.Masking;

namespace Octopus.Shared.Tests.Security
{
    [TestFixture]
    public class SensitiveDataMaskMultiLineFixture
    {
        [Test]
        public void ShouldMaskMultiLineValue()
        {
            const string sensitive = "Show me\nthe monkey!";

            string[] raw = { "Show me", "the monkey!" };

            var sdm = new SensitiveDataMask();
            var trie = CreateTrie(sensitive);
            var result = "";

            foreach (var line in raw)
                sdm.ApplyTo(trie,
                    line,
                    sanitized =>
                    {
                        result += sanitized;
                    });

            Assert.AreEqual(SensitiveDataMask.Mask, result);
        }

        [Test]
        public void ShouldNotMaskPartialMultiLineMatches()
        {
            const string sensitive = "Humpty Dumpty sat on a wall\nHumpty Dumpty had a great fall\nAll the kings horses and all the kings men\nCouldn't put Humpty together again";

            string[] raw = { "Humpty Dumpty sat on a wall", "Humpty Dumpty had a great fall", "All the kings horses and all the kings men", "Something something" };

            var sdm = new SensitiveDataMask();
            var trie = CreateTrie(sensitive);
            var result = "";

            foreach (var line in raw)
                sdm.ApplyTo(trie,
                    line,
                    sanitized =>
                    {
                        result += sanitized;
                    });

            Assert.AreEqual(string.Concat(raw), result);
        }

        [Test]
        public void ShouldMaskMultiLineValueNotAligned()
        {
            const string sensitive = "Humpty Dumpty sat on a wall\nHumpty Dumpty had a great fall\nAll the kings horses and all the kings men\nCouldn't put Humpty together again";

            string[] raw = { "<Message>Humpty Dumpty sat on a wall", "Humpty Dumpty had a great fall", "All the kings horses and all the kings men", "Couldn't put Humpty together again</Message>" };

            var sdm = new SensitiveDataMask();
            var trie = CreateTrie(sensitive);
            var result = "";

            foreach (var line in raw)
                sdm.ApplyTo(trie,
                    line,
                    sanitized =>
                    {
                        result += sanitized;
                    });

            // There are two instances of the Mask because these will be output by different actions.
            // i.e. logged to different lines
            Assert.AreEqual("<Message>" + SensitiveDataMask.Mask + SensitiveDataMask.Mask + "</Message>", result);
        }

        [Test]
        public void ShouldMaskSingleAndMultiLineCombined()
        {
            const string singleLineSensitive = "single line secret";
            const string multiLineSensitive = "multi\nline\nsecret";

            string[] raw = { "Single Line Secret: " + singleLineSensitive + " Multi-line Secret: " + "multi", "line", "secret" };

            var sdm = new SensitiveDataMask();
            var trie = CreateTrie(singleLineSensitive, multiLineSensitive);
            var result = "";

            foreach (var line in raw)
                sdm.ApplyTo(trie,
                    line,
                    sanitized =>
                    {
                        result += sanitized;
                    });

            Assert.AreEqual("Single Line Secret: " + SensitiveDataMask.Mask + " Multi-line Secret: " + SensitiveDataMask.Mask, result);
        }

        [Test]
        public void ShouldMaskMultiLineSurroundedByPartialMatches()
        {
            const string multiLineSensitive = "multi\nline\nsecret";

            string[] raw = { "multi", "multi", "line", "secret", "secret" };

            var sdm = new SensitiveDataMask();
            var trie = CreateTrie(multiLineSensitive);
            var result = "";

            foreach (var line in raw)
                sdm.ApplyTo(trie,
                    line,
                    sanitized =>
                    {
                        result += sanitized;
                    });

            Assert.AreEqual("multi" + SensitiveDataMask.Mask + "secret", result);
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