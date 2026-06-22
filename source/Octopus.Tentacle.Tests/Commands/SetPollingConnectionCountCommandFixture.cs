using System;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class SetPollingConnectionCountCommandFixture : CommandFixture<SetPollingConnectionCountCommand>
    {
        StubTentacleConfiguration tentacleConfiguration;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            tentacleConfiguration = new StubTentacleConfiguration();
            var log = Substitute.For<ISystemLog>();
            var selector = Substitute.For<IApplicationInstanceSelector>();
            selector.Current.Returns(info => new ApplicationInstanceConfiguration(null, null!, null!, null!));
            Command = new SetPollingConnectionCountCommand(
                new Lazy<IWritableTentacleConfiguration>(() => tentacleConfiguration),
                selector,
                log,
                Substitute.For<ILogFileOnlyLogger>());
        }

        [Test]
        public void ShouldSetThePollingConnectionCount()
        {
            Start("--pollingConnectionCount=5");
            tentacleConfiguration.PollingConnectionCount.Should().Be(5);
        }

        [Test]
        public void ShouldFailWhenNoCountProvided()
        {
            Action start = () => Start();
            start.Should().Throw<ControlledFailureException>();
            tentacleConfiguration.PollingConnectionCount.Should().BeNull();
        }

        [Test]
        public void ShouldFailWhenCountIsLessThanOne()
        {
            Action start = () => Start("--pollingConnectionCount=0");
            start.Should().Throw<ControlledFailureException>();
            tentacleConfiguration.PollingConnectionCount.Should().BeNull();
        }

        [Test]
        public void ShouldFailWithAFriendlyErrorWhenCountIsNotANumber()
        {
            Action start = () => Start("--pollingConnectionCount=notanumber");
            start.Should().Throw<ControlledFailureException>().WithMessage("*not a valid whole number*");
            tentacleConfiguration.PollingConnectionCount.Should().BeNull();
        }
    }
}
