using System;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Tests.Kubernetes
{
    [TestFixture]
    public class PodLogParserFixture
    {
        [TestCase("a|b|c", Reason = "Doesn't have 4 parts")]
        public void NotCorrectlyPipeDelimited(string line)
        {
            var result = PodLogParser.ParseLine(line);
            result.Error.Should().Contain("delimited");
        }
        
        [TestCase("a|b|c|d", Reason = "Not a line number")]
        public void FirstPartIsNotALineNumber(string line)
        {
            var result = PodLogParser.ParseLine(line);
            result.Error.Should().Contain("line number");
        }
    }
}