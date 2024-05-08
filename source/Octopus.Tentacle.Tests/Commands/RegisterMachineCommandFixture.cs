using System;
using System.Linq;
using System.Net;
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
using Octopus.Diagnostics;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class RegisterMachineCommandFixture : CommandFixture<RegisterMachineCommand>
    {
        IWritableTentacleConfiguration configuration;
        ISystemLog log;
        X509Certificate2 certificate;
        IRegisterMachineOperation operation;
        IOctopusServerChecker serverChecker;
        IOctopusAsyncRepository repository;
        string serverThumbprint;

        [SetUp]
        public void BeforeEachTest()
        {
            serverThumbprint = Guid.NewGuid().ToString();
            configuration = Substitute.For<IWritableTentacleConfiguration>();
            operation = Substitute.For<IRegisterMachineOperation>();
            serverChecker = Substitute.For<IOctopusServerChecker>();
            log = Substitute.For<ISystemLog>();
            var octopusClientInitializer = Substitute.For<IOctopusClientInitializer>();
            var octopusAsyncClient = Substitute.For<IOctopusAsyncClient>();

            repository = Substitute.For<IOctopusAsyncRepository>();
            repository.Client.Returns(octopusAsyncClient);
            repository.LoadRootDocument(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new RootResource { Version = "3.0" }));
            octopusAsyncClient.Repository.Returns(repository);
            octopusAsyncClient.ForSystem().Returns(repository);
            octopusAsyncClient.ForSpace(Arg.Any<SpaceResource>()).Returns(repository);

            var certificateConfigurationRepository = Substitute.For<ICertificateConfigurationRepository>();
            var certificateConfigurationResource = new CertificateConfigurationResource { Thumbprint = serverThumbprint };
            certificateConfigurationRepository.GetOctopusCertificate().Returns(Task.FromResult(certificateConfigurationResource));
            repository.CertificateConfiguration.Returns(certificateConfigurationRepository);
            octopusClientInitializer.CreateClient(Arg.Any<ApiEndpointOptions>(), false)
                .Returns(Task.FromResult(octopusAsyncClient));
            var selector = Substitute.For<IApplicationInstanceSelector>();
            selector.Current.Returns(info => new ApplicationInstanceConfiguration(null, null!, null!, null!));
            Command = new RegisterMachineCommand(new Lazy<IRegisterMachineOperation>(() => operation),
                                                 new Lazy<IWritableTentacleConfiguration>(() => configuration),
                                                 log,
                                                 selector,
                                                 new Lazy<IOctopusServerChecker>(() => serverChecker),
                                                 new ProxyConfigParser(),
                                                 octopusClientInitializer,
                                                 new SpaceRepositoryFactory(),
                                                 Substitute.For<ILogFileOnlyLogger>());

            configuration.ServicesPortNumber.Returns(90210);
            certificate = new CertificateGenerator(new NullLog()).GenerateNew("CN=Hello");
            configuration.TentacleCertificate.Returns(certificate);
        }

        [Test]
        public void ShouldRegisterListeningTentacle()
        {
            Start("--env=Development",
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

            Assert.That(operation.Environments.Single(), Is.EqualTo("Development"));
            Assert.That(operation.MachineName, Is.EqualTo("MyMachine"));
            Assert.That(operation.TentacleHostname, Is.EqualTo("mymachine.test"));
            Assert.That(operation.TentaclePort, Is.EqualTo(90210));
            Assert.That(operation.TentacleThumbprint, Is.EqualTo(certificate.Thumbprint));
            Assert.That(operation.AllowOverwrite, Is.True);
            Assert.That(operation.CommunicationStyle, Is.EqualTo(CommunicationStyle.TentaclePassive));
            Assert.That(operation.MachinePolicy, Is.Null);
            Assert.That(operation.Roles.First(), Is.EqualTo("app-server"));
            Assert.That(operation.Roles.Last(), Is.EqualTo("web-server"));
            Assert.That(operation.SubscriptionId, Is.Null);
            Assert.That(operation.TenantTags.Single(), Is.EqualTo("CustomerType/VIP"));
            Assert.That(operation.Tenants.Single(), Is.EqualTo("Tenant1"));
            Assert.That(operation.ProxyName, Is.EqualTo("Proxy"));

            configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Address == null &&
                                                        x.CommunicationStyle == CommunicationStyle.TentaclePassive &&
                                                        x.Thumbprint == serverThumbprint));

            operation.Received().ExecuteAsync(repository);
        }

        [Test]
        public void ShouldRegisterPollingTentacle()
        {
            AssertPollingTentacleRegistered("https://localhost:10943/", "--server-comms-port=10943");
        }

        [Test]
        public void ShouldRegisterPollingTentacleOverDefaultPort()
        {
            AssertPollingTentacleRegistered("https://localhost:10943/");
        }

        [Test]
        public void ShouldRegisterPollingTentacleOverCustomCommsPort()
        {
            AssertPollingTentacleRegistered("https://localhost:123/", "--server-comms-port=123");
        }

        [Test]
        public void ShouldRegisterPollingTentacleOverCustomCommsAddressAndServerCommsPort()
        {
            AssertPollingTentacleRegistered(
                "https://polling.localhost:456/",
                "--server-comms-address=https://polling.localhost:123/",
                "--server-comms-port=456");
        }

        [Test]
        public void ShouldRegisterPollingTentacleOverCustomCommsAddressWithPort()
        {
            AssertPollingTentacleRegistered(
                "https://polling.localhost:123/",
                "--server-comms-address=https://polling.localhost:123/");
        }

        [Test]
        public void ShouldRegisterPollingTentacleOverCustomCommsAddressWithDefaultHttpsPort()
        {
            AssertPollingTentacleRegistered(
                "https://polling.localhost/",
                "--server-comms-address=https://polling.localhost/");
        }

        void AssertPollingTentacleRegistered(string expectedServerAddress, params string[] additionalArgs)
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

            Start(args);

            Assert.That(operation.Environments.Single(), Is.EqualTo("Development"));
            Assert.That(operation.MachineName, Is.EqualTo("MyMachine"));
            Assert.That(operation.TentacleHostname, Is.Empty);
            Assert.That(operation.TentaclePort, Is.EqualTo(0));
            Assert.That(operation.TentacleThumbprint, Is.EqualTo(certificate.Thumbprint));
            Assert.That(operation.AllowOverwrite, Is.True);
            Assert.That(operation.CommunicationStyle, Is.EqualTo(CommunicationStyle.TentacleActive));
            Assert.That(operation.MachinePolicy, Is.Null);
            Assert.That(operation.Roles.First(), Is.EqualTo("app-server"));
            Assert.That(operation.Roles.Last(), Is.EqualTo("web-server"));
            operation.SubscriptionId.ToString().Should().StartWith("poll://");
            Assert.That(operation.TenantTags.Single(), Is.EqualTo("CustomerType/VIP"));
            Assert.That(operation.Tenants.Single(), Is.EqualTo("Tenant1"));

            configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Address.ToString() == expectedServerAddress &&
                                                   x.SubscriptionId == operation.SubscriptionId.ToString() &&
                                                   x.CommunicationStyle == CommunicationStyle.TentacleActive &&
                                                   x.Thumbprint == serverThumbprint));

            operation.Received().ExecuteAsync(repository);
        }

        [Test]
        public void ShouldReuseSubscriptionIdForPollingTentacleIfReRegistering()
        {
            var subscriptionId = "poll://xz6h25sh28shx52/";
            var octopusServerConfiguration = new OctopusServerConfiguration(serverThumbprint)
            {
                SubscriptionId = subscriptionId,
                CommunicationStyle = CommunicationStyle.TentacleActive,
                Address = new Uri("https://localhost:10943/")
            };
            configuration.TrustedOctopusServers.Returns(new[] {octopusServerConfiguration});

            Start("--env=Development",
                  "--server=http://localhost",
                  "--name=MyMachine",
                  "--apiKey=ABC123",
                  "--force",
                  "--role=app-server",
                  "--comms-style=TentacleActive",
                  "--server-comms-port=10943");

            Assert.That(operation.CommunicationStyle, Is.EqualTo(CommunicationStyle.TentacleActive));
            Assert.That(operation.SubscriptionId.ToString(), Is.EqualTo(subscriptionId));

            configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Address.ToString() == "https://localhost:10943/" &&
                                                   x.SubscriptionId == subscriptionId &&
                                                   x.CommunicationStyle == CommunicationStyle.TentacleActive &&
                                                   x.Thumbprint == serverThumbprint));

            operation.Received().ExecuteAsync(repository);
        }

        [Test]
        public void DoesntReuseSubscriptionIdForPollingTentacleIfNewRegistration()
        {
            var subscriptionId = "poll://xz6h25sh28shx52/";
            var octopusServerConfiguration = new OctopusServerConfiguration(serverThumbprint)
            {
                SubscriptionId = subscriptionId,
                CommunicationStyle = CommunicationStyle.TentacleActive,
                Address = new Uri("https://octopus.example.com:10943/")
            };
            configuration.TrustedOctopusServers.Returns(new[] {octopusServerConfiguration});

            Start("--env=Development",
                  "--server=http://localhost", //different server
                  "--name=MyMachine",
                  "--apiKey=ABC123",
                  "--force",
                  "--role=app-server",
                  "--comms-style=TentacleActive",
                  "--server-comms-port=10943");

            Assert.That(operation.CommunicationStyle, Is.EqualTo(CommunicationStyle.TentacleActive));
            Assert.That(operation.SubscriptionId.ToString(), Is.Not.EqualTo(subscriptionId));

            configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Address.ToString() == "https://localhost:10943/" &&
                                                   x.SubscriptionId != subscriptionId &&
                                                   x.CommunicationStyle == CommunicationStyle.TentacleActive &&
                                                   x.Thumbprint == serverThumbprint));

            operation.Received().ExecuteAsync(repository);
        }
        
        [Test]
        public void ShouldRegisterPollingTentacleWithCustomServerSubscriptionId()
        {
            var subscriptionId = "poll://xz6h25sh28shx52/";

            Start("--env=Development",
                "--server=http://localhost",
                "--name=MyMachine",
                "--apiKey=ABC123",
                "--force",
                "--role=app-server",
                "--comms-style=TentacleActive",
                "--server-comms-port=10943",
                $"--server-subscription-id={subscriptionId}"
                );

            Assert.That(operation.CommunicationStyle, Is.EqualTo(CommunicationStyle.TentacleActive));
            Assert.That(operation.SubscriptionId.ToString(), Is.EqualTo(subscriptionId));

            configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Address.ToString() == "https://localhost:10943/" &&
                    x.SubscriptionId == subscriptionId &&
                    x.CommunicationStyle == CommunicationStyle.TentacleActive &&
                    x.Thumbprint == serverThumbprint));

            operation.Received().ExecuteAsync(repository);
        }
    }
}