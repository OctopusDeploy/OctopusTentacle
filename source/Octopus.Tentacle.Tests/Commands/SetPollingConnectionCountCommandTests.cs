using System;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Commands.Support;

namespace Octopus.Tentacle.Tests.Commands
{
    public class SetPollingConnectionCountCommandTests
    {
        [Test]
        public void ShouldSetThePollingConnectionCount()
        {
            var tentacleConfiguration = new StubTentacleConfiguration();
            var command = CommandBuilder.SetPollingConnectionCount(tentacleConfiguration);

            command.StartWith("--pollingConnectionCount=5");
            tentacleConfiguration.PollingConnectionCount.Should().Be(5);
        }

        [Test]
        public void ShouldFailWhenNoCountProvided()
        {
            var tentacleConfiguration = new StubTentacleConfiguration();
            var command = CommandBuilder.SetPollingConnectionCount(tentacleConfiguration);

            Action start = () => command.StartWith();
            start.Should().Throw<ControlledFailureException>();
            tentacleConfiguration.PollingConnectionCount.Should().BeNull();
        }

        [Test]
        public void ShouldFailWhenCountIsLessThanOne()
        {
            var tentacleConfiguration = new StubTentacleConfiguration();
            var command = CommandBuilder.SetPollingConnectionCount(tentacleConfiguration);

            Action start = () => command.StartWith("--pollingConnectionCount=0");
            start.Should().Throw<ControlledFailureException>();
            tentacleConfiguration.PollingConnectionCount.Should().BeNull();
        }

        [Test]
        public void ShouldFailWithAFriendlyErrorWhenCountIsNotANumber()
        {
            var tentacleConfiguration = new StubTentacleConfiguration();
            var command = CommandBuilder.SetPollingConnectionCount(tentacleConfiguration);

            Action start = () => command.StartWith("--pollingConnectionCount=notanumber");
            start.Should().Throw<ControlledFailureException>().WithMessage("*not a valid whole number*");
            tentacleConfiguration.PollingConnectionCount.Should().BeNull();
        }
    }
}
