using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Tests.Contracts
{
    [TestFixture]
    public class ScriptTicketFixture
    {
        [Test]
        public void GetHashCodeShouldIgnoreCasing()
        {
            var originalScriptTicket = new ScriptTicket("MixedCasing");
            var upperCaseScriptTicket = new ScriptTicket(originalScriptTicket.TaskId.ToUpper());
            var lowerCaseScriptTicket = new ScriptTicket(originalScriptTicket.TaskId.ToLower());

            upperCaseScriptTicket.GetHashCode().Should().Be(originalScriptTicket.GetHashCode());
            lowerCaseScriptTicket.GetHashCode().Should().Be(originalScriptTicket.GetHashCode());
        }

        [Test]
        public void EqualityShouldIgnoreCasing()
        {
            var originalScriptTicket = new ScriptTicket("MixedCasing");
            var upperCaseScriptTicket = new ScriptTicket(originalScriptTicket.TaskId.ToUpper());
            var lowerCaseScriptTicket = new ScriptTicket(originalScriptTicket.TaskId.ToLower());

            upperCaseScriptTicket.Should().Be(originalScriptTicket);
            lowerCaseScriptTicket.Should().Be(originalScriptTicket);
        }
    }
}