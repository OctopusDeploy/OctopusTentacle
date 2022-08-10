using System;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Tests
{
    [TestFixture]
    class ArgsParsing
    {
        [Test]
        public void Test()
        {
            var options = OctopusProgram.ParseCommandHostArgumentsFromCommandLineArguments(
                new[] { "--noninteractive" },
                out var console,
                out var noninteractive,
                out _);
            noninteractive.Should().BeTrue();
            options.Should().BeEmpty();
        }
    }
}