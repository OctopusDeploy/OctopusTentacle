using System;
using System.Collections.Generic;
using FluentAssertions;
using FluentAssertions.Execution;
using NUnit.Framework;
using Octopus.Tentacle.Contracts.Builders;

namespace Octopus.Tentacle.Tests.Contracts
{
    [TestFixture]
    public class UniqueScriptTicketBuilderFixture
    {
        [Test]
        public void ShouldCreateShortScriptTickets()
        {
            using var scope = new AssertionScope();

            for (var i = 0; i < 1000; i++)
            {
                var scriptTicket = new UniqueScriptTicketBuilder().Build();

                scriptTicket.TaskId.Length.Should().BeGreaterOrEqualTo(10).And.BeLessOrEqualTo(22, "Script Ticket should be short as it is used as the name of a folder by Tentacle");
            }
        }

        [Test]
        public void ShouldCreateUniqueScriptTickets()
        {
            var scriptTickets = new List<string>();

            for (var i = 0; i < 1000; i++)
            {
                var scriptTicket = new UniqueScriptTicketBuilder().Build();

                scriptTickets.Add(scriptTicket.TaskId);
            }

            scriptTickets.Should().OnlyHaveUniqueItems("Script Ticket should be unique as Tentacle uses it as the identifier for the executing script");
        }

        [Test]
        public void ShouldCreateAlphaNumericScriptTickets()
        {
            using var scope = new AssertionScope();

            for (var i = 0; i < 1000; i++)
            {
                var scriptTicket = new UniqueScriptTicketBuilder().Build();

                scriptTicket.TaskId.Should().MatchRegex("[a-zA-Z0-9]", "Script Ticket should be safe to use as the name of a folder on the file system");
            }
        }
    }
}