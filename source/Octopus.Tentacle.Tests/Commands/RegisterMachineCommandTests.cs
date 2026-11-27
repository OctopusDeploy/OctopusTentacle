using System;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Client.Operations;
using Octopus.Client.Repositories.Async;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Commands.Support;
using Octopus.Tentacle.Tests.Support.TestAttributes;

namespace Octopus.Tentacle.Tests.Commands
{
    public class RegisterMachineCommandTests
    {
        const string WebSocketAddress = "wss://polling.localhost/OctopusComms";
        const string WebSocketSslThumbprint = "76B1C4A6F1E2B3C4D5E6F708192A3B4C5D6E7F80";

        [Test]
        public void ShouldRegisterListeningTentacle()
        {
            var registration = new Registration("3.0");
            var command = CreateCommand(registration, OperatorWhoTrusts());

            command.StartWith("--env=Development",
                  "--server=http://localhost",
                  "--name=MyMachine",
                  "--publicHostName=mymachine.test",
                  "--apiKey=ABC123",
                  "--force",
                  "--proxy=Proxy",
                  "--role=app-server",
                  "--role=web-server",
                  "--tenant=Tenant1",
                  "--tenantTag=CustomerType/VIP");

            Assert.That(registration.Operation.Environments.Single(), Is.EqualTo("Development"));
            Assert.That(registration.Operation.MachineName, Is.EqualTo("MyMachine"));
            Assert.That(registration.Operation.TentacleHostname, Is.EqualTo("mymachine.test"));
            Assert.That(registration.Operation.TentaclePort, Is.EqualTo(90210));
            Assert.That(registration.Operation.TentacleThumbprint, Is.EqualTo(registration.Certificate.Thumbprint));
            Assert.That(registration.Operation.AllowOverwrite, Is.True);
            Assert.That(registration.Operation.CommunicationStyle, Is.EqualTo(CommunicationStyle.TentaclePassive));
            Assert.That(registration.Operation.MachinePolicy, Is.Null);
            Assert.That(registration.Operation.Roles.First(), Is.EqualTo("app-server"));
            Assert.That(registration.Operation.Roles.Last(), Is.EqualTo("web-server"));
            Assert.That(registration.Operation.SubscriptionId, Is.Null);
            Assert.That(registration.Operation.TenantTags.Single(), Is.EqualTo("CustomerType/VIP"));
            Assert.That(registration.Operation.Tenants.Single(), Is.EqualTo("Tenant1"));
            Assert.That(registration.Operation.ProxyName, Is.EqualTo("Proxy"));

            registration.Configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Address == null &&
                                                        x.CommunicationStyle == CommunicationStyle.TentaclePassive &&
                                                        x.Thumbprint == registration.ServerThumbprint));

            registration.Operation.Received().ExecuteAsync(registration.Repository);
        }

        [Test]
        public void ShouldRegisterPollingTentacle()
        {
            var registration = new Registration("3.0");
            var command = CreateCommand(registration, OperatorWhoTrusts());

            AssertPollingTentacleRegistered(registration, command, "https://localhost:10943/", "--server-comms-port=10943");
        }

        [Test]
        public void ShouldRegisterPollingTentacleOverDefaultPort()
        {
            var registration = new Registration("3.0");
            var command = CreateCommand(registration, OperatorWhoTrusts());

            AssertPollingTentacleRegistered(registration, command, "https://localhost:10943/");
        }

        [Test]
        public void ShouldRegisterPollingTentacleOverCustomCommsPort()
        {
            var registration = new Registration("3.0");
            var command = CreateCommand(registration, OperatorWhoTrusts());

            AssertPollingTentacleRegistered(registration, command, "https://localhost:123/", "--server-comms-port=123");
        }

        [Test]
        public void ShouldRegisterPollingTentacleOverCustomCommsAddressAndServerCommsPort()
        {
            var registration = new Registration("3.0");
            var command = CreateCommand(registration, OperatorWhoTrusts());

            AssertPollingTentacleRegistered(registration, command, 
                "https://polling.localhost:456/",
                "--server-comms-address=https://polling.localhost:123/",
                "--server-comms-port=456");
        }

        [Test]
        public void ShouldRegisterPollingTentacleOverCustomCommsAddressWithPort()
        {
            var registration = new Registration("3.0");
            var command = CreateCommand(registration, OperatorWhoTrusts());

            AssertPollingTentacleRegistered(registration, command, 
                "https://polling.localhost:123/",
                "--server-comms-address=https://polling.localhost:123/");
        }

        [Test]
        public void ShouldRegisterPollingTentacleOverCustomCommsAddressWithDefaultHttpsPort()
        {
            var registration = new Registration("3.0");
            var command = CreateCommand(registration, OperatorWhoTrusts());

            AssertPollingTentacleRegistered(registration, command, 
                "https://polling.localhost/",
                "--server-comms-address=https://polling.localhost/");
        }

        [Test]
        public void ShouldDefaultPollingConnectionCountToOneWhenRegisteringAPollingTentacle()
        {
            var registration = new Registration("3.0");
            var command = CreateCommand(registration, OperatorWhoTrusts());

            AssertPollingTentacleRegistered(registration, command, "https://localhost:10943/");
            registration.Configuration.Received().SetPollingConnectionCount(1);
        }

        [Test]
        public void ShouldNotSetPollingConnectionCountWhenRegisteringAListeningTentacle()
        {
            var registration = new Registration("3.0");
            var command = CreateCommand(registration, OperatorWhoTrusts());

            command.StartWith("--env=Development",
                "--server=http://localhost",
                "--name=MyMachine",
                "--publicHostName=mymachine.test",
                "--apiKey=ABC123",
                "--force",
                "--role=app-server");

            registration.Configuration.DidNotReceive().SetPollingConnectionCount(Arg.Any<int>());
        }

        static void AssertPollingTentacleRegistered(Registration registration, ICommand command, string expectedServerAddress, params string[] additionalArgs)
        {
            var args = new []
            {
                "--env=Development",
                "--server=http://localhost",
                "--name=MyMachine",
                "--publicHostName=mymachine.test",
                "--apiKey=ABC123",
                "--force",
                "--role=app-server",
                "--role=web-server",
                "--tenant=Tenant1",
                "--tenantTag=CustomerType/VIP",
                "--comms-style=TentacleActive",
            };

            args = args.Concat(additionalArgs).ToArray();

            command.StartWith(args);

            Assert.That(registration.Operation.Environments.Single(), Is.EqualTo("Development"));
            Assert.That(registration.Operation.MachineName, Is.EqualTo("MyMachine"));
            Assert.That(registration.Operation.TentacleHostname, Is.Empty);
            Assert.That(registration.Operation.TentaclePort, Is.EqualTo(0));
            Assert.That(registration.Operation.TentacleThumbprint, Is.EqualTo(registration.Certificate.Thumbprint));
            Assert.That(registration.Operation.AllowOverwrite, Is.True);
            Assert.That(registration.Operation.CommunicationStyle, Is.EqualTo(CommunicationStyle.TentacleActive));
            Assert.That(registration.Operation.MachinePolicy, Is.Null);
            Assert.That(registration.Operation.Roles.First(), Is.EqualTo("app-server"));
            Assert.That(registration.Operation.Roles.Last(), Is.EqualTo("web-server"));
            registration.Operation.SubscriptionId.ToString().Should().StartWith("poll://");
            Assert.That(registration.Operation.TenantTags.Single(), Is.EqualTo("CustomerType/VIP"));
            Assert.That(registration.Operation.Tenants.Single(), Is.EqualTo("Tenant1"));

            registration.Configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Address.ToString() == expectedServerAddress &&
                                                   x.SubscriptionId == registration.Operation.SubscriptionId.ToString() &&
                                                   x.CommunicationStyle == CommunicationStyle.TentacleActive &&
                                                   x.Thumbprint == registration.ServerThumbprint));

            registration.Operation.Received().ExecuteAsync(registration.Repository);
        }

        [Test]
        [WindowsTest]
        public void ShouldTrustAWebSocketServerWhoseCertificateValidates()
        {
            var registration = new Registration("3.0");
            var trustPrompt = OperatorWhoTrusts();
            var command = CreateCommand(registration, trustPrompt);

            StartWebSocketRegistration(registration, command, SslPolicyErrors.None);

            trustPrompt.DidNotReceiveWithAnyArgs().Confirm(default!, default!);

            registration.Configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Thumbprint == WebSocketSslThumbprint &&
                    x.CommunicationStyle == CommunicationStyle.TentacleActive));
        }

        [Test]
        [WindowsTest]
        public void ShouldNotTrustAWebSocketServerWhoseCertificateFailsValidationWhenTheOperatorDeclines()
        {
            var registration = new Registration("3.0");
            var trustPrompt = OperatorWhoTrusts();
            var command = CreateCommand(registration, trustPrompt);

            trustPrompt.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

            Assert.Throws<ControlledFailureException>(() => StartWebSocketRegistration(registration, command, SslPolicyErrors.RemoteCertificateChainErrors));

            registration.Configuration.DidNotReceiveWithAnyArgs().AddOrUpdateTrustedOctopusServer(default!);
            registration.Operation.DidNotReceive().ExecuteAsync(registration.Repository);
        }

        [Test]
        [WindowsTest]
        public void ShouldNotTrustAWebSocketServerWhoseCertificateFailsValidationWhenNobodyCanBeAsked()
        {
            var registration = new Registration("3.0");
            var trustPrompt = OperatorWhoTrusts();
            var command = CreateCommand(registration, trustPrompt);

            trustPrompt.CanPrompt.Returns(false);

            Assert.Throws<ControlledFailureException>(() => StartWebSocketRegistration(registration, command, SslPolicyErrors.RemoteCertificateChainErrors));

            registration.Configuration.DidNotReceiveWithAnyArgs().AddOrUpdateTrustedOctopusServer(default!);
        }

        [Test]
        [WindowsTest]
        public void ShouldTrustAWebSocketServerWhoseCertificateFailsValidationWhenItsThumbprintWasSuppliedUpFront()
        {
            var registration = new Registration("3.0");
            var trustPrompt = OperatorWhoTrusts();
            var command = CreateCommand(registration, trustPrompt);

            StartWebSocketRegistration(registration, command, SslPolicyErrors.RemoteCertificateChainErrors,
                $"--server-web-socket-thumbprint={WebSocketSslThumbprint}");

            trustPrompt.DidNotReceiveWithAnyArgs().Confirm(default!, default!);

            registration.Configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Thumbprint == WebSocketSslThumbprint));
        }

        [Test]
        [WindowsTest]
        public void ShouldNotTrustAWebSocketServerWhoseCertificateDoesNotMatchTheSuppliedThumbprint()
        {
            var registration = new Registration("3.0");
            var command = CreateCommand(registration, OperatorWhoTrusts());

            Assert.Throws<ControlledFailureException>(() => StartWebSocketRegistration(registration, command, SslPolicyErrors.None,
                "--server-web-socket-thumbprint=0123456789ABCDEF0123456789ABCDEF01234567"));

            registration.Configuration.DidNotReceiveWithAnyArgs().AddOrUpdateTrustedOctopusServer(default!);
        }

        [Test]
        public void ShouldRejectAServerWebSocketThumbprintWithoutAServerWebSocketAddress()
        {
            var registration = new Registration("3.0");
            var command = CreateCommand(registration, OperatorWhoTrusts());

            var ex = Assert.Throws<ControlledFailureException>(() => command.StartWith("--env=Development",
                "--server=http://localhost",
                "--name=MyMachine",
                "--apiKey=ABC123",
                "--force",
                "--role=app-server",
                "--comms-style=TentacleActive",
                $"--server-web-socket-thumbprint={WebSocketSslThumbprint}"));

            ex!.Message.Should().Be("Option --server-web-socket-thumbprint can only be used with --server-web-socket.");
        }

        [Test]
        public void ShouldReuseSubscriptionIdForPollingTentacleIfReRegistering()
        {
            var registration = new Registration("3.0");
            var command = CreateCommand(registration, OperatorWhoTrusts());

            var subscriptionId = "poll://xz6h25sh28shx52/";
            var octopusServerConfiguration = new OctopusServerConfiguration(registration.ServerThumbprint)
            {
                SubscriptionId = subscriptionId,
                CommunicationStyle = CommunicationStyle.TentacleActive,
                Address = new Uri("https://localhost:10943/")
            };
            registration.Configuration.TrustedOctopusServers.Returns(new[] {octopusServerConfiguration});

            command.StartWith("--env=Development",
                  "--server=http://localhost",
                  "--name=MyMachine",
                  "--apiKey=ABC123",
                  "--force",
                  "--role=app-server",
                  "--comms-style=TentacleActive",
                  "--server-comms-port=10943");

            Assert.That(registration.Operation.CommunicationStyle, Is.EqualTo(CommunicationStyle.TentacleActive));
            Assert.That(registration.Operation.SubscriptionId.ToString(), Is.EqualTo(subscriptionId));

            registration.Configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Address.ToString() == "https://localhost:10943/" &&
                                                   x.SubscriptionId == subscriptionId &&
                                                   x.CommunicationStyle == CommunicationStyle.TentacleActive &&
                                                   x.Thumbprint == registration.ServerThumbprint));

            registration.Operation.Received().ExecuteAsync(registration.Repository);
        }

        [Test]
        public void DoesntReuseSubscriptionIdForPollingTentacleIfNewRegistration()
        {
            var registration = new Registration("3.0");
            var command = CreateCommand(registration, OperatorWhoTrusts());

            var subscriptionId = "poll://xz6h25sh28shx52/";
            var octopusServerConfiguration = new OctopusServerConfiguration(registration.ServerThumbprint)
            {
                SubscriptionId = subscriptionId,
                CommunicationStyle = CommunicationStyle.TentacleActive,
                Address = new Uri("https://octopus.example.com:10943/")
            };
            registration.Configuration.TrustedOctopusServers.Returns(new[] {octopusServerConfiguration});

            command.StartWith("--env=Development",
                  "--server=http://localhost", //different server
                  "--name=MyMachine",
                  "--apiKey=ABC123",
                  "--force",
                  "--role=app-server",
                  "--comms-style=TentacleActive",
                  "--server-comms-port=10943");

            Assert.That(registration.Operation.CommunicationStyle, Is.EqualTo(CommunicationStyle.TentacleActive));
            Assert.That(registration.Operation.SubscriptionId.ToString(), Is.Not.EqualTo(subscriptionId));

            registration.Configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Address.ToString() == "https://localhost:10943/" &&
                                                   x.SubscriptionId != subscriptionId &&
                                                   x.CommunicationStyle == CommunicationStyle.TentacleActive &&
                                                   x.Thumbprint == registration.ServerThumbprint));

            registration.Operation.Received().ExecuteAsync(registration.Repository);
        }
        
        [Test]
        public void ShouldRegisterPollingTentacleWithCustomServerSubscriptionId()
        {
            var registration = new Registration("3.0");
            var command = CreateCommand(registration, OperatorWhoTrusts());

            var subscriptionId = "poll://xz6h25sh28shx52/";

            command.StartWith("--env=Development",
                "--server=http://localhost",
                "--name=MyMachine",
                "--apiKey=ABC123",
                "--force",
                "--role=app-server",
                "--comms-style=TentacleActive",
                "--server-comms-port=10943",
                $"--server-subscription-id={subscriptionId}"
                );

            Assert.That(registration.Operation.CommunicationStyle, Is.EqualTo(CommunicationStyle.TentacleActive));
            Assert.That(registration.Operation.SubscriptionId.ToString(), Is.EqualTo(subscriptionId));

            registration.Configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Address.ToString() == "https://localhost:10943/" &&
                    x.SubscriptionId == subscriptionId &&
                    x.CommunicationStyle == CommunicationStyle.TentacleActive &&
                    x.Thumbprint == registration.ServerThumbprint));

            registration.Operation.Received().ExecuteAsync(registration.Repository);
        }
        
        [Test]
        public void ShouldThrowExceptionWhenCustomServerSubscriptionIdDoesNotHaveCorrectScheme()
        {
            var registration = new Registration("3.0");
            var command = CreateCommand(registration, OperatorWhoTrusts());

            var ex = Assert.Throws<ControlledFailureException>(() => 
            command.StartWith("--env=Development",
                "--server=http://localhost",
                "--name=MyMachine",
                "--apiKey=ABC123",
                "--force",
                "--role=app-server",
                "--comms-style=TentacleActive",
                "--server-comms-port=10943",
                "--server-subscription-id=https://xz6h25sh28shx52/"
            ));
            ex.Message.Should().Be("The ServerSubscriptionId must start with poll://");
        }
        
        static void StartWebSocketRegistration(Registration registration, ICommand command, SslPolicyErrors certificateErrors, params string[] additionalArgs)
        {
            registration.ServerChecker.CheckServerCommunicationsIsOpen(Arg.Any<Uri>(), Arg.Any<IWebProxy>())
                .Returns(new OctopusServerCommunicationsCheckResult(WebSocketSslThumbprint, certificateErrors));

            var args = new[]
            {
                "--env=Development",
                "--server=http://localhost",
                "--name=MyMachine",
                "--apiKey=ABC123",
                "--force",
                "--role=app-server",
                "--comms-style=TentacleActive",
                $"--server-web-socket={WebSocketAddress}"
            };

            command.StartWith(args.Concat(additionalArgs).ToArray());
        }

        static IPrompt OperatorWhoTrusts()
        {
            var trustPrompt = Substitute.For<IPrompt>();
            trustPrompt.CanPrompt.Returns(true);
            trustPrompt.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
            return trustPrompt;
        }

        static RegisterMachineCommand CreateCommand(Registration registration, IPrompt trustPrompt)
        {
            return new RegisterMachineCommand(new Lazy<IRegisterMachineOperation>(() => registration.Operation),
                new Lazy<IWritableTentacleConfiguration>(() => registration.Configuration),
                registration.Log,
                UnnamedInstanceSelector(),
                new Lazy<IOctopusServerChecker>(() => registration.ServerChecker),
                new ProxyConfigParser(),
                registration.ClientInitializer,
                new SpaceRepositoryFactory(),
                Substitute.For<ILogFileOnlyLogger>(),
                new ServerCertificateTrustConfirmation(registration.Log, trustPrompt));
        }

        class Registration
        {
            public Registration(string serverVersion)
            {
                ServerThumbprint = Guid.NewGuid().ToString();
                Configuration = Substitute.For<IWritableTentacleConfiguration>();
                Operation = Substitute.For<IRegisterMachineOperation>();
                ServerChecker = Substitute.For<IOctopusServerChecker>();
                Log = Substitute.For<ISystemLog>();
                ClientInitializer = Substitute.For<IOctopusClientInitializer>();
                var octopusAsyncClient = Substitute.For<IOctopusAsyncClient>();

                Repository = Substitute.For<IOctopusAsyncRepository>();
                Repository.Client.Returns(octopusAsyncClient);
                Repository.LoadRootDocument(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new RootResource { Version = serverVersion }));
                octopusAsyncClient.Repository.Returns(Repository);
                octopusAsyncClient.ForSystem().Returns(Repository);
                octopusAsyncClient.ForSpace(Arg.Any<SpaceResource>()).Returns(Repository);

                var certificateConfigurationRepository = Substitute.For<ICertificateConfigurationRepository>();
                var certificateConfigurationResource = new CertificateConfigurationResource { Thumbprint = ServerThumbprint };
                certificateConfigurationRepository.GetOctopusCertificate().Returns(Task.FromResult(certificateConfigurationResource));
                Repository.CertificateConfiguration.Returns(certificateConfigurationRepository);
                ClientInitializer.CreateClient(Arg.Any<ApiEndpointOptions>(), false)
                    .Returns(Task.FromResult(octopusAsyncClient));

                Configuration.ServicesPortNumber.Returns(90210);
                Certificate = new CertificateGenerator(new NullLog()).GenerateNew("CN=Hello");
                Configuration.TentacleCertificate.Returns(Certificate);
            }

            public string ServerThumbprint { get; }
            public IWritableTentacleConfiguration Configuration { get; }
            public IRegisterMachineOperation Operation { get; }
            public IOctopusServerChecker ServerChecker { get; }
            public ISystemLog Log { get; }
            public IOctopusClientInitializer ClientInitializer { get; }
            public IOctopusAsyncRepository Repository { get; }
            public X509Certificate2 Certificate { get; }
        }

        static IApplicationInstanceSelector UnnamedInstanceSelector()
        {
            var selector = Substitute.For<IApplicationInstanceSelector>();
            selector.Current.Returns(_ => new ApplicationInstanceConfiguration(null, null!, null!, null!, null));
            return selector;
        }
    }
}
