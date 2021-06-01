using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Client.Model;
using Octopus.Shared;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Internals.Options;
using Octopus.Shared.Startup;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Tests.Support;

namespace Octopus.Tentacle.Tests.Commands
{
    public class ServerCommsCommandTest
    {
        const string Thumb1 = "Thumbprint1";
        const string Thumb2 = "Thumbprint2";

        ICommand command;
        StubTentacleConfiguration configuration;

        [SetUp]
        public void SetUp()
        {
            configuration = new StubTentacleConfiguration();
            var selector = Substitute.For<IApplicationInstanceSelector>();
            selector.Current.Returns(info => new ApplicationInstanceConfiguration(null, null!, null!, null!));
            command = new ServerCommsCommand(
                new Lazy<IWritableTentacleConfiguration>(() => configuration),
                new InMemoryLog(),
                selector,
                Substitute.For<ILogFileOnlyLogger>()
                );
        }

        void Execute(string thumbprint, CommunicationStyle style, string host = null, string port = null)
        {
            var parameters = new List<string>
            {
                $"--thumbprint={thumbprint}",
                $"--style={style}"
            };

            if (host != null)
                parameters.Add($"--host={host}");
            if (port != null)
                parameters.Add($"--port={port}");

            command.Start(
                parameters.ToArray(),
                Substitute.For<ICommandRuntime>(),
                new OptionSet()
            );
        }

        void AddTrusts(params string[] thumbprints)
        {
            configuration.TrustedOctopusThumbprints = thumbprints;
        }

        static void Assert(OctopusServerConfiguration server, string thumbprint, CommunicationStyle style, Uri address)
        {
            server.Thumbprint.Should().Be(thumbprint);
            server.CommunicationStyle.Should().Be(style);
            server.Address.Should().Be(address);

            if (style == CommunicationStyle.TentacleActive)
                server.SubscriptionId.Should().NotBeEmpty();
        }


        [Test]
        public void NoTrusts()
        {
            Action action = () => Execute(Thumb1, CommunicationStyle.TentaclePassive);
            action.Should().Throw<ControlledFailureException>()
                .WithMessage("Before server communications can be modified, trust must be established with the configure command");
        }

        [Test]
        public void AddPassive()
        {
            AddTrusts(Thumb1);
            Execute(Thumb1, CommunicationStyle.TentaclePassive);

            configuration.TrustedOctopusServers.Should().HaveCount(1);
            var server = configuration.TrustedOctopusServers.First();
            Assert(server, Thumb1, CommunicationStyle.TentaclePassive, null);
        }

        [Test]
        public void AddActiveNoHost()
        {
            AddTrusts(Thumb1);
            Action action = () => Execute(Thumb1, CommunicationStyle.TentacleActive, null, "1234");
            action.Should().Throw<ControlledFailureException>()
                .WithMessage("Please provide either the server hostname or websocket address, e.g. --host=OCTOPUS");
        }

        [Test]
        public void AddActiveNoPort()
        {
            AddTrusts(Thumb1);

            Execute(Thumb1, CommunicationStyle.TentacleActive, "example.com", null);

            configuration.TrustedOctopusServers.Should().HaveCount(1);
            var server = configuration.TrustedOctopusServers.First();
            Assert(server, Thumb1, CommunicationStyle.TentacleActive, new Uri("https://example.com:10943"));
        }

        [Test]
        public void AddActive()
        {
            AddTrusts(Thumb1);

            Execute(Thumb1, CommunicationStyle.TentacleActive, "example.com", "1234");

            configuration.TrustedOctopusServers.Should().HaveCount(1);
            var server = configuration.TrustedOctopusServers.First();
            Assert(server, Thumb1, CommunicationStyle.TentacleActive, new Uri("https://example.com:1234"));
        }

        [Test]
        public void AddPassive1ThenActive1()
        {
            AddTrusts(Thumb1);

            Execute(Thumb1, CommunicationStyle.TentaclePassive);
            Execute(Thumb1, CommunicationStyle.TentacleActive, "example.com", "1234");

            var servers = configuration.TrustedOctopusServers.ToArray();
            servers.Should().HaveCount(2);
            Assert(servers[0], Thumb1, CommunicationStyle.TentaclePassive, null);
            Assert(servers[1], Thumb1, CommunicationStyle.TentacleActive, new Uri("https://example.com:1234"));
        }

        [Test]
        public void AddActive1ThenPassive1()
        {
            AddTrusts(Thumb1);

            Execute(Thumb1, CommunicationStyle.TentacleActive, "example.com", "1234");
            Execute(Thumb1, CommunicationStyle.TentaclePassive);

            var servers = configuration.TrustedOctopusServers.ToArray();
            servers.Should().HaveCount(2);
            Assert(servers[0], Thumb1, CommunicationStyle.TentacleActive, new Uri("https://example.com:1234"));
            Assert(servers[1], Thumb1, CommunicationStyle.TentaclePassive, null);
        }

        [Test]
        public void AddPassive1ThenActive2()
        {
            AddTrusts(Thumb1, Thumb2);

            Execute(Thumb1, CommunicationStyle.TentaclePassive);
            Execute(Thumb2, CommunicationStyle.TentacleActive, "example.com", "1234");

            var servers = configuration.TrustedOctopusServers.ToArray();
            servers.Should().HaveCount(2);
            Assert(servers[0], Thumb1, CommunicationStyle.TentaclePassive, null);
            Assert(servers[1], Thumb2, CommunicationStyle.TentacleActive, new Uri("https://example.com:1234"));
        }

        [Test]
        public void AddPassive1ThenPassive2()
        {
            AddTrusts(Thumb1, Thumb2);

            Execute(Thumb1, CommunicationStyle.TentaclePassive);
            Execute(Thumb2, CommunicationStyle.TentaclePassive);

            var servers = configuration.TrustedOctopusServers.ToArray();
            servers.Should().HaveCount(2);
            Assert(servers[0], Thumb1, CommunicationStyle.TentaclePassive, null);
            Assert(servers[1], Thumb2, CommunicationStyle.TentaclePassive, null);
        }

        [Test]
        public void AddActive1ThenActive2()
        {
            AddTrusts(Thumb1, Thumb2);

            Execute(Thumb1, CommunicationStyle.TentacleActive, "example.com", "1234");
            Execute(Thumb2, CommunicationStyle.TentacleActive, "foo.com", "1234");

            var servers = configuration.TrustedOctopusServers.ToArray();
            servers.Should().HaveCount(2);
            Assert(servers[0], Thumb1, CommunicationStyle.TentacleActive, new Uri("https://example.com:1234"));
            Assert(servers[1], Thumb2, CommunicationStyle.TentacleActive, new Uri("https://foo.com:1234"));
        }

        [Test]
        public void AddActive1ThenActive1SameAddress()
        {
            AddTrusts(Thumb1);

            Execute(Thumb1, CommunicationStyle.TentacleActive, "example.com", "1234");
            Execute(Thumb1, CommunicationStyle.TentacleActive, "EXAMPLE.cOm", "1234");

            configuration.TrustedOctopusServers.Should().HaveCount(1);
            var server = configuration.TrustedOctopusServers.First();
            Assert(server, Thumb1, CommunicationStyle.TentacleActive, new Uri("https://example.com:1234"));
        }

        [Test]
        public void AddActive1ThenActive1DifferentAddress()
        {
            AddTrusts(Thumb1);

            Execute(Thumb1, CommunicationStyle.TentacleActive, "example.com", "1234");
            Execute(Thumb1, CommunicationStyle.TentacleActive, "foo.com", "1234");

            var servers = configuration.TrustedOctopusServers.ToArray();
            servers.Should().HaveCount(2);
            Assert(servers[0], Thumb1, CommunicationStyle.TentacleActive, new Uri("https://example.com:1234"));
            Assert(servers[1], Thumb1, CommunicationStyle.TentacleActive, new Uri("https://foo.com:1234"));
        }
    }
}