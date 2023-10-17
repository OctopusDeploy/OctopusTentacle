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
        public void WhenScriptTicketsAreDifferentThenHashCodeShouldNotBeSame()
        {
            var scriptTicket1 = new ScriptTicket("one");
            var scriptTicket2 = new ScriptTicket("two");

            scriptTicket1.GetHashCode().Should().NotBe(scriptTicket2.GetHashCode());
        }

        [Test]
        public void EqualityShouldIgnoreCasing()
        {
            var originalScriptTicket = new ScriptTicket("MixedCasing");
            var upperCaseScriptTicket = new ScriptTicket(originalScriptTicket.TaskId.ToUpper());
            var lowerCaseScriptTicket = new ScriptTicket(originalScriptTicket.TaskId.ToLower());

            originalScriptTicket.Equals(upperCaseScriptTicket).Should().BeTrue();
            originalScriptTicket.Equals(lowerCaseScriptTicket).Should().BeTrue();

            originalScriptTicket.Equals((object)upperCaseScriptTicket).Should().BeTrue();
            originalScriptTicket.Equals((object)lowerCaseScriptTicket).Should().BeTrue();

            (originalScriptTicket == upperCaseScriptTicket).Should().BeTrue();
            (originalScriptTicket == lowerCaseScriptTicket).Should().BeTrue();

            (originalScriptTicket != upperCaseScriptTicket).Should().BeFalse();
            (originalScriptTicket != lowerCaseScriptTicket).Should().BeFalse();
        }

        [Test]
        public void WhenScriptTicketsAreDifferentThenShouldNotBeEqual()
        {
            var scriptTicket1 = new ScriptTicket("one");
            var scriptTicket2 = new ScriptTicket("two");

            scriptTicket1.Equals(scriptTicket2).Should().BeFalse();

            scriptTicket1.Equals((object)scriptTicket2).Should().BeFalse();

            (scriptTicket1 == scriptTicket2).Should().BeFalse();

            (scriptTicket1 != scriptTicket2).Should().BeTrue();
        }
    }
}