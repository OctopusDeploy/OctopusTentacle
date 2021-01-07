using System;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Client.Model;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Util;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class ConfigureCommandFixture : CommandFixture<ConfigureCommand>
    {
        StubTentacleConfiguration tentacleConfiguration;
        IOctopusFileSystem fileSystem;
        ISystemLog log;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            tentacleConfiguration = new StubTentacleConfiguration();
            fileSystem = Substitute.For<IOctopusFileSystem>();
            log = Substitute.For<ISystemLog>();
            Command = new ConfigureCommand(new Lazy<IWritableTentacleConfiguration>(() => tentacleConfiguration), new Lazy<IWritableHomeConfiguration>(() => new StubHomeConfiguration()), fileSystem, log, Substitute.For<IApplicationInstanceSelector>());
        }

        [Test]
        public void ShouldSetPort()
        {
            Start("--port=90210");
            Assert.That(tentacleConfiguration.ServicesPortNumber, Is.EqualTo(90210));
        }

        [Test]
        public void ShouldSetApplicationDirectory()
        {
            fileSystem.GetFullPath(Arg.Any<string>()).Returns("C:\\Apps");
            Start("--appdir=Apps");
            Assert.That(tentacleConfiguration.ApplicationDirectory, Is.EqualTo("C:\\Apps"));
        }

        [Test]
        public void ShouldResetTrust()
        {
            tentacleConfiguration.TrustedOctopusThumbprints = new[] { "ABC123", "DEF456" };
            Start("--reset-trust");
            CollectionAssert.AreEqual(tentacleConfiguration.TrustedOctopusThumbprints, new string[0]);
        }

        [Test]
        public void ShouldAddAndRemoveTrust()
        {
            tentacleConfiguration.TrustedOctopusThumbprints = new[] { "ABC123", "DEF456" };
            Start("--trust=GHI789", "--remove-trust=ABC123");
            CollectionAssert.AreEqual(tentacleConfiguration.TrustedOctopusThumbprints, new[] { "DEF456", "GHI789" });
        }

        [Test]
        public void ShouldSetCommunicationStyleToPassiveByDefault()
        {
            Start("--trust=GHI789");
            tentacleConfiguration.TrustedOctopusServers.First().CommunicationStyle.Should().Be(CommunicationStyle.TentaclePassive);
        }

        [Test]
        public void ShouldSetCommunicationStyleToPassiveIfNoListenIsFalse()
        {
            Start("--noListen=false", "--trust=GHI789");
            tentacleConfiguration.TrustedOctopusServers.First().CommunicationStyle.Should().Be(CommunicationStyle.TentaclePassive);
        }


        [Test]
        public void ShouldSetCommunicationStyleToActiveIfNoListenIsTrue()
        {
            Start("--noListen=true", "--trust=GHI789");
            tentacleConfiguration.TrustedOctopusServers.First().CommunicationStyle.Should().Be(CommunicationStyle.TentacleActive);
        }
    }
}