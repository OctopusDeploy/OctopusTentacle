using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Client.Scripts.Models.Builders;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Tests
{
    [TestFixture]
    public class ExecuteShellScriptCommandBuilderTests
    {
        [Test]
        public void WhenScriptTicket_ItOverridesTheGeneratedSriptTicket()
        {
            var scriptTicket = new ScriptTicket("arealticket");
            new ExecuteShellScriptCommandBuilder("bob", ScriptIsolationLevel.FullIsolation)
                .WithScriptTicket(scriptTicket)
                .Build()
                .ScriptTicket.Should().Be(scriptTicket);
        }
    }
}