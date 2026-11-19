using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Client.Model.Endpoints;
using Octopus.Client.Repositories.Async;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Support;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class ReplaceCertificateCommandFixture : CommandFixture<ReplaceCertificateCommand>
    {
        const string ServerThumbprint = "SERVERTHUMBPRINT";

        TemporaryDirectory temporaryDirectory;
        WritableTentacleConfiguration configuration;
        IOctopusAsyncRepository repository;

        [SetUp]
        public void BeforeEachTest()
        {
            var fileSystem = new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>());
            temporaryDirectory = new TemporaryDirectory(fileSystem);
            var configFile = Path.Combine(temporaryDirectory.DirectoryPath, "Tentacle.config");
            File.WriteAllText(configFile, "<?xml version=\"1.0\" encoding=\"utf-8\"?><octopus-settings></octopus-settings>");
            var store = new XmlFileKeyValueStore(fileSystem, configFile);

            var selector = Substitute.For<IApplicationInstanceSelector>();
            selector.Current.Returns(_ => new ApplicationInstanceConfiguration(null, configFile, store, store));

            configuration = new WritableTentacleConfiguration(
                selector,
                Substitute.For<IHomeConfiguration>(),
                new CertificateGenerator(new NullLog()),
                Substitute.For<IProxyConfiguration>(),
                Substitute.For<IPollingProxyConfiguration>(),
                Substitute.For<ISystemLog>());

            repository = Substitute.For<IOctopusAsyncRepository>();
            var certificateConfiguration = Substitute.For<ICertificateConfigurationRepository>();
            certificateConfiguration.GetOctopusCertificate().Returns(Task.FromResult(new CertificateConfigurationResource { Thumbprint = ServerThumbprint }));
            repository.CertificateConfiguration.Returns(certificateConfiguration);

            var client = Substitute.For<IOctopusAsyncClient>();
            client.Repository.Returns(repository);
            client.ForSystem().Returns(repository);
            var clientInitializer = Substitute.For<IOctopusClientInitializer>();
            clientInitializer.CreateClient(Arg.Any<ApiEndpointOptions>(), Arg.Any<bool>()).Returns(Task.FromResult(client));

            Command = new ReplaceCertificateCommand(
                new Lazy<IWritableTentacleConfiguration>(() => configuration),
                clientInitializer,
                new SpaceRepositoryFactory(),
                Substitute.For<ISystemLog>(),
                selector,
                Substitute.For<ILogFileOnlyLogger>());
        }

        [TearDown]
        public void AfterEachTest() => temporaryDirectory.Dispose();

        [Test]
        public void ShouldOnlyChangeTheThumbprintOfAPollingWorkerAndTrustTheServer()
        {
            var endpoint = new PollingTentacleEndpointResource { Uri = "poll://abc123/", Thumbprint = "OLD" };
            var worker = new WorkerResource { Id = "Workers-1", Name = "MyWorker", Endpoint = endpoint, WorkerPoolIds = new ReferenceCollection("WorkerPools-1") };
            repository.Workers.FindByName("MyWorker", Arg.Any<CancellationToken>()).Returns(Task.FromResult(worker));

            Start("--server=http://octopus.test", "--apiKey=API-KEY", "--name=MyWorker");

            var newThumbprint = configuration.TentacleCertificate!.Thumbprint;
            newThumbprint.Should().NotBe("OLD");
            endpoint.Thumbprint.Should().Be(newThumbprint);
            endpoint.Uri.Should().Be("poll://abc123/");
            worker.WorkerPoolIds.Should().Contain("WorkerPools-1");

            var server = configuration.TrustedOctopusServers.Should().ContainSingle().Subject;
            server.Thumbprint.Should().Be(ServerThumbprint);
            server.CommunicationStyle.Should().Be(CommunicationStyle.TentacleActive);
            server.Address.Should().Be(new Uri("https://octopus.test:10943"));
            server.SubscriptionId.Should().Be("poll://abc123/");
        }

        [Test]
        public void ShouldRefuseAnAmbiguousNameRatherThanGuessBetweenATargetAndAWorker()
        {
            repository.Machines.FindByName("Shared", Arg.Any<CancellationToken>()).Returns(Task.FromResult(new MachineResource { Name = "Shared", Endpoint = new ListeningTentacleEndpointResource() }));
            repository.Workers.FindByName("Shared", Arg.Any<CancellationToken>()).Returns(Task.FromResult(new WorkerResource { Name = "Shared", Endpoint = new ListeningTentacleEndpointResource() }));

            Action start = () => Start("--server=http://octopus.test", "--apiKey=API-KEY", "--name=Shared");

            start.Should().Throw<ControlledFailureException>().WithMessage("*--id*");
            configuration.TentacleCertificate.Should().BeNull();
        }
    }
}
