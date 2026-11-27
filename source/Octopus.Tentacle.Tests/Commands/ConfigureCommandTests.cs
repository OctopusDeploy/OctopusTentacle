using System;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Client.Model;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Commands.Support;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Commands
{
    public class ConfigureCommandTests
    {
        [Test]
        public void ShouldSetPort()
        {
            var tentacleConfiguration = new StubTentacleConfiguration();
            var command = CreateCommand(tentacleConfiguration);

            command.StartWith("--port=90210");
            Assert.That(tentacleConfiguration.ServicesPortNumber, Is.EqualTo(90210));
        }

        [Test]
        public void ShouldSetPollingConnectionCount()
        {
            var tentacleConfiguration = new StubTentacleConfiguration();
            var command = CreateCommand(tentacleConfiguration);

            command.StartWith("--pollingConnectionCount=7");
            tentacleConfiguration.PollingConnectionCount.Should().Be(7);
        }

        [Test]
        public void ShouldRejectAPollingConnectionCountLessThanOne()
        {
            var tentacleConfiguration = new StubTentacleConfiguration();
            var command = CreateCommand(tentacleConfiguration);

            Action start = () => command.StartWith("--pollingConnectionCount=0");
            start.Should().Throw<ControlledFailureException>();
            tentacleConfiguration.PollingConnectionCount.Should().BeNull();
        }

        [Test]
        public void ShouldRejectAPollingConnectionCountThatIsNotANumber()
        {
            var tentacleConfiguration = new StubTentacleConfiguration();
            var command = CreateCommand(tentacleConfiguration);

            Action start = () => command.StartWith("--pollingConnectionCount=notanumber");
            start.Should().Throw<ControlledFailureException>().WithMessage("*not a valid whole number*");
            tentacleConfiguration.PollingConnectionCount.Should().BeNull();
        }

        [Test]
        public void ShouldSetApplicationDirectory()
        {
            var tentacleConfiguration = new StubTentacleConfiguration();
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            fileSystem.GetFullPath(Arg.Any<string>()).Returns("C:\\Apps");
            var command = CreateCommand(tentacleConfiguration, fileSystem);

            command.StartWith("--appdir=Apps");
            Assert.That(tentacleConfiguration.ApplicationDirectory, Is.EqualTo("C:\\Apps"));
        }

        [Test]
        public void ShouldResetTrust()
        {
            var tentacleConfiguration = new StubTentacleConfiguration { TrustedOctopusThumbprints = new[] { "ABC123", "DEF456" } };
            var command = CreateCommand(tentacleConfiguration);

            command.StartWith("--reset-trust");
            CollectionAssert.AreEqual(tentacleConfiguration.TrustedOctopusThumbprints, new string[0]);
        }

        [Test]
        public void ShouldAddAndRemoveTrust()
        {
            var tentacleConfiguration = new StubTentacleConfiguration { TrustedOctopusThumbprints = new[] { "ABC123", "DEF456" } };
            var command = CreateCommand(tentacleConfiguration);

            command.StartWith("--trust=GHI789", "--remove-trust=ABC123");
            CollectionAssert.AreEqual(tentacleConfiguration.TrustedOctopusThumbprints, new[] { "DEF456", "GHI789" });
        }

        [Test]
        public void ShouldSetCommunicationStyleToPassiveByDefault()
        {
            var tentacleConfiguration = new StubTentacleConfiguration();
            var command = CreateCommand(tentacleConfiguration);

            command.StartWith("--trust=GHI789");
            tentacleConfiguration.TrustedOctopusServers.First().CommunicationStyle.Should().Be(CommunicationStyle.TentaclePassive);
        }

        [Test]
        public void ShouldSetCommunicationStyleToPassiveIfNoListenIsFalse()
        {
            var tentacleConfiguration = new StubTentacleConfiguration();
            var command = CreateCommand(tentacleConfiguration);

            command.StartWith("--noListen=false", "--trust=GHI789");
            tentacleConfiguration.TrustedOctopusServers.First().CommunicationStyle.Should().Be(CommunicationStyle.TentaclePassive);
        }

        [Test]
        public void ShouldSetCommunicationStyleToActiveIfNoListenIsTrue()
        {
            var tentacleConfiguration = new StubTentacleConfiguration();
            var command = CreateCommand(tentacleConfiguration);

            command.StartWith("--noListen=true", "--trust=GHI789");
            tentacleConfiguration.TrustedOctopusServers.First().CommunicationStyle.Should().Be(CommunicationStyle.TentacleActive);
        }

        static ConfigureCommand CreateCommand(StubTentacleConfiguration configuration, IOctopusFileSystem? fileSystem = null)
        {
            return new ConfigureCommand(new Lazy<IWritableTentacleConfiguration>(() => configuration),
                new Lazy<IWritableHomeConfiguration>(() => new StubHomeConfiguration()),
                fileSystem ?? Substitute.For<IOctopusFileSystem>(),
                Substitute.For<ISystemLog>(),
                UnnamedInstanceSelector(),
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
