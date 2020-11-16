using System;
using NUnit.Framework;
using Octopus.Shared.Internals.DiffMatchPatch;

namespace Octopus.Shared.Tests.Internals.DiffMatchPatch
{
    [TestFixture]
    public class GoogleDiffMatchPatchFixture
    {
        [Test]
        public void ChangedCharacter_Highlighted()
        {
            var before = "{\r\nFirstValue = 11}";
            var after = "{\r\nFirstValue = 12}";

            var diffEngine = new GoogleDiffMatchPatch();
            var diffs = diffEngine.DiffMain(before, after);
            diffEngine.diff_cleanupSemantic(diffs);
            var actual = diffEngine.diff_prettyHtml(diffs);

            var expected = "<span>{&para;<br>FirstValue = 1</span><del style=\"background:#ffe6e6;\">1</del><ins style=\"background:#e6ffe6;\">2</ins><span>}</span>";
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void NoChange_NoHighlighting()
        {
            var before = "{\r\nFirstValue = 11}";
            var after = "{\r\nFirstValue = 11}";

            var diffEngine = new GoogleDiffMatchPatch();
            var diffs = diffEngine.DiffMain(before, after);
            diffEngine.diff_cleanupSemantic(diffs);
            var actual = diffEngine.diff_prettyHtml(diffs);

            var expected = "<span>{&para;<br>FirstValue = 11}</span>";
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void LineBreakChange_LineBreakCharacterHighlighted()
        {
            var before = "{\nFirstValue = 11}";
            var after = "{\r\nFirstValue = 11}";

            var diffEngine = new GoogleDiffMatchPatch();
            var diffs = diffEngine.DiffMain(before, after);
            diffEngine.diff_cleanupSemantic(diffs);
            var actual = diffEngine.diff_prettyHtml(diffs);

            var expected = "<span>{</span><ins style=\"background:#e6ffe6;\">\r</ins><span>&para;<br>FirstValue = 11}</span>";
            Assert.AreEqual(expected, actual);
        }
    }
}