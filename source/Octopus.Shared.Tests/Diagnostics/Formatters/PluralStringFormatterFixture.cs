using System;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Shared.Diagnostics.Formatters;

namespace Octopus.Shared.Tests.Diagnostics.Formatters
{
    public class PluralStringFormatterFixture
    {
        [Test]
        public void ShouldPluralize()
        {
            var actual = string.Format(new PluralStringFormatter(), "{0:n0} {0:p:car}", 2);
            actual.Should().BeEquivalentTo("2 cars");
        }

        [Test]
        public void ShouldWorkWithoutFormatSpecified()
        {
            var actual = string.Format(new PluralStringFormatter(), "{0} {0:p:car}", 2);
            actual.Should().BeEquivalentTo("2 cars");
        }
    }
}