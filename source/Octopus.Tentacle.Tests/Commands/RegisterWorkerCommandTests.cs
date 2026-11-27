using System;
using System.Linq;
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

namespace Octopus.Tentacle.Tests.Commands
{
    public class RegisterWorkerCommandTests
    {
        [Test]
        public void ShouldRegisterListeningWorker()
        {
            var registration = new Registration("2018.4");
            var command = CreateCommand(registration);

            command.StartWith("--workerpool=SomePool",
                "--server=http://localhost",
                "--name=MyMachine",
                "--publicHostName=mymachine.test",
                "--apiKey=ABC123",
                "--force",
                "--proxy=Proxy");

            Assert.That(registration.Operation.WorkerPools.Single(), Is.EqualTo("SomePool"));
            Assert.That(registration.Operation.MachineName, Is.EqualTo("MyMachine"));
            Assert.That(registration.Operation.TentacleHostname, Is.EqualTo("mymachine.test"));
            Assert.That(registration.Operation.TentaclePort, Is.EqualTo(90210));
            Assert.That(registration.Operation.TentacleThumbprint, Is.EqualTo(registration.Certificate.Thumbprint));
            Assert.That(registration.Operation.AllowOverwrite, Is.True);
            Assert.That(registration.Operation.CommunicationStyle, Is.EqualTo(CommunicationStyle.TentaclePassive));
            Assert.That(registration.Operation.MachinePolicy, Is.Null);
            Assert.That(registration.Operation.SubscriptionId, Is.Null);
            Assert.That(registration.Operation.ProxyName, Is.EqualTo("Proxy"));

            registration.Configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Address == null &&
                    x.CommunicationStyle == CommunicationStyle.TentaclePassive &&
                    x.Thumbprint == registration.ServerThumbprint));

            registration.Operation.Received().ExecuteAsync(registration.Repository);
        }

        [Test]
        public void ShouldRegisterPollingWorker()
        {
            var registration = new Registration("2018.4");
            var command = CreateCommand(registration);

            AssertPollingWorkerRegistered(registration, command, "https://localhost:10943/", "--server-comms-port=10943");
        }

        [Test]
        public void ShouldRegisterPollingWorkerOverDefaultPort()
        {
            var registration = new Registration("2018.4");
            var command = CreateCommand(registration);

            AssertPollingWorkerRegistered(registration, command, "https://localhost:10943/");
        }

        [Test]
        public void ShouldRegisterPollingWorkerOverCustomCommsPort()
        {
            var registration = new Registration("2018.4");
            var command = CreateCommand(registration);

            AssertPollingWorkerRegistered(registration, command, "https://localhost:123/", "--server-comms-port=123");
        }

        [Test]
        public void ShouldRegisterPollingWorkerOverCustomCommsAddressAndServerCommsPort()
        {
            var registration = new Registration("2018.4");
            var command = CreateCommand(registration);

            AssertPollingWorkerRegistered(registration, command, 
                "https://polling.localhost:456/",
                "--server-comms-address=https://polling.localhost:123/",
                "--server-comms-port=456");
        }

        [Test]
        public void ShouldRegisterPollingWorkerOverCustomCommsAddressWithPort()
        {
            var registration = new Registration("2018.4");
            var command = CreateCommand(registration);

            AssertPollingWorkerRegistered(registration, command, 
                "https://polling.localhost:123/",
                "--server-comms-address=https://polling.localhost:123/");
        }

        [Test]
        public void ShouldRegisterPollingWorkerOverCustomCommsAddressWithDefaultHttpsPort()
        {
            var registration = new Registration("2018.4");
            var command = CreateCommand(registration);

            AssertPollingWorkerRegistered(registration, command, 
                "https://polling.localhost/",
                "--server-comms-address=https://polling.localhost/");
        }

        // no need for tests like ShouldReuseSubscriptionIdForPollingTentacleIfReRegistering
        // because it runs the same code for Workers and Deployment targets

        [Test]
        public void ShouldDefaultPollingConnectionCountWhenRegisteringAPollingWorker()
        {
            var registration = new Registration("2018.4");
            var command = CreateCommand(registration);

            var expectedConnectionCount = RegisterWorkerCommandBase<IRegisterWorkerOperation>.DefaultPollingConnectionCountFor(Environment.ProcessorCount);

            AssertPollingWorkerRegistered(registration, command, "https://localhost:10943/");
            registration.Configuration.Received().SetPollingConnectionCount(expectedConnectionCount);
        }

        [TestCase(1, 5)]
        [TestCase(5, 5)]
        [TestCase(16, 16)]
        [TestCase(128, 128)]
        [TestCase(129, 128)]
        [TestCase(512, 128)]
        public void DefaultPollingConnectionCountScalesWithProcessorsWithinBounds(int processorCount, int expected)
        {
            RegisterWorkerCommandBase<IRegisterWorkerOperation>.DefaultPollingConnectionCountFor(processorCount).Should().Be(expected);
        }

        [Test]
        public void ShouldNotSetPollingConnectionCountWhenRegisteringAListeningWorker()
        {
            var registration = new Registration("2018.4");
            var command = CreateCommand(registration);

            command.StartWith("--workerpool=SomePool",
                "--server=http://localhost",
                "--name=MyMachine",
                "--publicHostName=mymachine.test",
                "--apiKey=ABC123",
                "--force");

            registration.Configuration.DidNotReceive().SetPollingConnectionCount(Arg.Any<int>());
        }

        [Test]
        public void ShouldNotOverridePollingConnectionCountWhenAlreadyConfigured()
        {
            var registration = new Registration("2018.4");
            var command = CreateCommand(registration);

            registration.Configuration.PollingConnectionCount.Returns(3);

            AssertPollingWorkerRegistered(registration, command, "https://localhost:10943/");

            registration.Configuration.DidNotReceive().SetPollingConnectionCount(Arg.Any<int>());
        }

        static void AssertPollingWorkerRegistered(Registration registration, ICommand command, string expectedServerAddress, params string[] additionalArgs)
        {
            var args = new []
            {
                "--workerpool=SomePool",
                "--server=http://localhost",
                "--name=MyMachine",
                "--publicHostName=mymachine.test",
                "--apiKey=ABC123",
                "--force",
                "--comms-style=TentacleActive",
            };

            args = args.Concat(additionalArgs).ToArray();

            command.StartWith(args);

            Assert.That(registration.Operation.WorkerPools.Single(), Is.EqualTo("SomePool"));
            Assert.That(registration.Operation.MachineName, Is.EqualTo("MyMachine"));
            Assert.That(registration.Operation.TentacleHostname, Is.Empty);
            Assert.That(registration.Operation.TentaclePort, Is.EqualTo(0));
            Assert.That(registration.Operation.TentacleThumbprint, Is.EqualTo(registration.Certificate.Thumbprint));
            Assert.That(registration.Operation.AllowOverwrite, Is.True);
            Assert.That(registration.Operation.CommunicationStyle, Is.EqualTo(CommunicationStyle.TentacleActive));
            Assert.That(registration.Operation.MachinePolicy, Is.Null);
            registration.Operation.SubscriptionId.ToString().Should().StartWith("poll://");

            registration.Configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Address.ToString() == expectedServerAddress &&
                    x.SubscriptionId == registration.Operation.SubscriptionId.ToString() &&
                    x.CommunicationStyle == CommunicationStyle.TentacleActive &&
                    x.Thumbprint == registration.ServerThumbprint));

            registration.Operation.Received().ExecuteAsync(registration.Repository);
        }

        static RegisterWorkerCommand CreateCommand(Registration registration)
        {
            return new RegisterWorkerCommand(new Lazy<IRegisterWorkerOperation>(() => registration.Operation),
                new Lazy<IWritableTentacleConfiguration>(() => registration.Configuration),
                registration.Log,
                UnnamedInstanceSelector(),
                new Lazy<IOctopusServerChecker>(() => registration.ServerChecker),
                new ProxyConfigParser(),
                registration.ClientInitializer,
                new SpaceRepositoryFactory(),
                Substitute.For<ILogFileOnlyLogger>(),
                Substitute.For<IServerCertificateTrustConfirmation>());
        }

        class Registration
        {
            public Registration(string serverVersion)
            {
                ServerThumbprint = Guid.NewGuid().ToString();
                Configuration = Substitute.For<IWritableTentacleConfiguration>();
                Operation = Substitute.For<IRegisterWorkerOperation>();
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
            public IRegisterWorkerOperation Operation { get; }
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
