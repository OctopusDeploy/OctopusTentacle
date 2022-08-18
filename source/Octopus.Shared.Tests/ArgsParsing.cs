using System;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Shared.Startup;

namespace Octopus.Shared.Tests
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