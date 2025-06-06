﻿using System;
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
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class RegisterWorkerCommandFixture : CommandFixture<RegisterWorkerCommand>
    {
        IWritableTentacleConfiguration configuration;
        ISystemLog log;
        X509Certificate2 certificate;
        IRegisterWorkerOperation operation;
        IOctopusServerChecker serverChecker;
        IOctopusAsyncRepository repository;
        string serverThumbprint;

        [SetUp]
        public void BeforeEachTest()
        {
            serverThumbprint = Guid.NewGuid().ToString();
            configuration = Substitute.For<IWritableTentacleConfiguration>();
            operation = Substitute.For<IRegisterWorkerOperation>();
            serverChecker = Substitute.For<IOctopusServerChecker>();
            log = Substitute.For<ISystemLog>();
            var octopusClientInitializer = Substitute.For<IOctopusClientInitializer>();
            var octopusAsyncClient = Substitute.For<IOctopusAsyncClient>();

            repository = Substitute.For<IOctopusAsyncRepository>();
            repository.Client.Returns(octopusAsyncClient);
            repository.LoadRootDocument(Arg.Any<CancellationToken>()).Returns(new RootResource { Version = "2018.4" });
            octopusAsyncClient.Repository.Returns(repository);
            octopusAsyncClient.ForSystem().Returns(repository);
            octopusAsyncClient.ForSpace(Arg.Any<SpaceResource>()).Returns(repository);

            var certificateConfigurationRepository = Substitute.For<ICertificateConfigurationRepository>();
            var certificateConfigurationResource = new CertificateConfigurationResource { Thumbprint = serverThumbprint };
            certificateConfigurationRepository.GetOctopusCertificate().Returns(Task.FromResult(certificateConfigurationResource));
            repository.CertificateConfiguration.Returns(certificateConfigurationRepository);
            octopusClientInitializer.CreateClient(Arg.Any<ApiEndpointOptions>(), false)
                .Returns(Task.FromResult(octopusAsyncClient));

            var applicationInstanceSelector = Substitute.For<IApplicationInstanceSelector>();
            applicationInstanceSelector.Current.Returns(info => new ApplicationInstanceConfiguration(null, null!, null!, null!));

            Command = new RegisterWorkerCommand(new Lazy<IRegisterWorkerOperation>(() => operation),
                new Lazy<IWritableTentacleConfiguration>(() => configuration),
                log,
                applicationInstanceSelector,
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
        public void ShouldRegisterListeningWorker()
        {
            Start("--workerpool=SomePool",
                "--server=http://localhost",
                "--name=MyMachine",
                "--publicHostName=mymachine.test",
                "--apiKey=ABC123",
                "--force",
                "--proxy=Proxy");

            Assert.That(operation.WorkerPools.Single(), Is.EqualTo("SomePool"));
            Assert.That(operation.MachineName, Is.EqualTo("MyMachine"));
            Assert.That(operation.TentacleHostname, Is.EqualTo("mymachine.test"));
            Assert.That(operation.TentaclePort, Is.EqualTo(90210));
            Assert.That(operation.TentacleThumbprint, Is.EqualTo(certificate.Thumbprint));
            Assert.That(operation.AllowOverwrite, Is.True);
            Assert.That(operation.CommunicationStyle, Is.EqualTo(CommunicationStyle.TentaclePassive));
            Assert.That(operation.MachinePolicy, Is.Null);
            Assert.That(operation.SubscriptionId, Is.Null);
            Assert.That(operation.ProxyName, Is.EqualTo("Proxy"));

            configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Address == null &&
                    x.CommunicationStyle == CommunicationStyle.TentaclePassive &&
                    x.Thumbprint == serverThumbprint));

            operation.Received().ExecuteAsync(repository);
        }

        [Test]
        public void ShouldRegisterPollingWorker()
        {
            AssertPollingWorkerRegistered("https://localhost:10943/", "--server-comms-port=10943");
        }

        [Test]
        public void ShouldRegisterPollingWorkerOverDefaultPort()
        {
            AssertPollingWorkerRegistered("https://localhost:10943/");
        }

        [Test]
        public void ShouldRegisterPollingWorkerOverCustomCommsPort()
        {
            AssertPollingWorkerRegistered("https://localhost:123/", "--server-comms-port=123");
        }

        [Test]
        public void ShouldRegisterPollingWorkerOverCustomCommsAddressAndServerCommsPort()
        {
            AssertPollingWorkerRegistered(
                "https://polling.localhost:456/",
                "--server-comms-address=https://polling.localhost:123/",
                "--server-comms-port=456");
        }

        [Test]
        public void ShouldRegisterPollingWorkerOverCustomCommsAddressWithPort()
        {
            AssertPollingWorkerRegistered(
                "https://polling.localhost:123/",
                "--server-comms-address=https://polling.localhost:123/");
        }

        [Test]
        public void ShouldRegisterPollingWorkerOverCustomCommsAddressWithDefaultHttpsPort()
        {
            AssertPollingWorkerRegistered(
                "https://polling.localhost/",
                "--server-comms-address=https://polling.localhost/");
        }

        // no need for tests like ShouldReuseSubscriptionIdForPollingTentacleIfReRegistering
        // because it runs the same code for Workers and Deployment targets

        void AssertPollingWorkerRegistered(string expectedServerAddress, params string[] additionalArgs)
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

            Start(args);

            Assert.That(operation.WorkerPools.Single(), Is.EqualTo("SomePool"));
            Assert.That(operation.MachineName, Is.EqualTo("MyMachine"));
            Assert.That(operation.TentacleHostname, Is.Empty);
            Assert.That(operation.TentaclePort, Is.EqualTo(0));
            Assert.That(operation.TentacleThumbprint, Is.EqualTo(certificate.Thumbprint));
            Assert.That(operation.AllowOverwrite, Is.True);
            Assert.That(operation.CommunicationStyle, Is.EqualTo(CommunicationStyle.TentacleActive));
            Assert.That(operation.MachinePolicy, Is.Null);
            operation.SubscriptionId.ToString().Should().StartWith("poll://");

            configuration.Received().AddOrUpdateTrustedOctopusServer(
                Arg.Is<OctopusServerConfiguration>(x => x.Address.ToString() == expectedServerAddress &&
                    x.SubscriptionId == operation.SubscriptionId.ToString() &&
                    x.CommunicationStyle == CommunicationStyle.TentacleActive &&
                    x.Thumbprint == serverThumbprint));

            operation.Received().ExecuteAsync(repository);
        }

    }
}