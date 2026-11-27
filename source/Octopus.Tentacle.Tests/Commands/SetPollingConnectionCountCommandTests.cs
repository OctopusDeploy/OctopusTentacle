using System;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
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
            var command = CreateCommand(tentacleConfiguration);

            command.StartWith("--pollingConnectionCount=5");
            tentacleConfiguration.PollingConnectionCount.Should().Be(5);
        }

        [Test]
        public void ShouldFailWhenNoCountProvided()
        {
            var tentacleConfiguration = new StubTentacleConfiguration();
            var command = CreateCommand(tentacleConfiguration);

            Action start = () => command.StartWith();
            start.Should().Throw<ControlledFailureException>();
            tentacleConfiguration.PollingConnectionCount.Should().BeNull();
        }

        [Test]
        public void ShouldFailWhenCountIsLessThanOne()
        {
            var tentacleConfiguration = new StubTentacleConfiguration();
            var command = CreateCommand(tentacleConfiguration);

            Action start = () => command.StartWith("--pollingConnectionCount=0");
            start.Should().Throw<ControlledFailureException>();
            tentacleConfiguration.PollingConnectionCount.Should().BeNull();
        }

        [Test]
        public void ShouldFailWithAFriendlyErrorWhenCountIsNotANumber()
        {
            var tentacleConfiguration = new StubTentacleConfiguration();
            var command = CreateCommand(tentacleConfiguration);

            Action start = () => command.StartWith("--pollingConnectionCount=notanumber");
            start.Should().Throw<ControlledFailureException>().WithMessage("*not a valid whole number*");
            tentacleConfiguration.PollingConnectionCount.Should().BeNull();
        }

        static SetPollingConnectionCountCommand CreateCommand(StubTentacleConfiguration configuration)
        {
            return new SetPollingConnectionCountCommand(new Lazy<IWritableTentacleConfiguration>(() => configuration),
                UnnamedInstanceSelector(),
                Substitute.For<ISystemLog>(),
                Substitute.For<ILogFileOnlyLogger>());
        }

        static IApplicationInstanceSelector UnnamedInstanceSelector()
        {
            var selector = Substitute.For<IApplicationInstanceSelector>();
            selector.Current.Returns(_ => new ApplicationInstanceConfiguration(null, null!, null!, null!, null));
            return selector;
        }
    }
}
