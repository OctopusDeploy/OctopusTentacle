using FluentAssertions;
using NUnit.Framework;
using Octopus.Manager.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Support
{
    public class StringExtensionTests
    {

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(" , a", "a")]
        [TestCase("a", "a")]
        [TestCase(" a ", "a")]
        [TestCase("a, b", "a", "b")]
        [TestCase("a, \"b c\",d", "a", "b c", "d")]
        [TestCase("a, \"b, c\",d", "a", "b, c", "d")]
        public void SplitOnSeperators(string input, params string[] expected)
            => input.SplitOnSeperators().Should().BeEquivalentTo(expected);
    }
}