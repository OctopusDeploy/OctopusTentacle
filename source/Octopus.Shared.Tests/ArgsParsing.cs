using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Shared.Internals.Options;
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
                new[] {"--noninteractive"},
                out var console,
                out var noninteractive, 
                out _);
            noninteractive.Should().BeTrue();
            options.Should().BeEmpty();
        }
    }

    
}
