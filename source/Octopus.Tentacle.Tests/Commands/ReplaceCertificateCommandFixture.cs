using System;
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

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class ReplaceCertificateCommandFixture : CommandFixture<ReplaceCertificateCommand>
    {
        const string ServerThumbprint = "SERVERTHUMBPRINT";

        IWritableTentacleConfiguration configuration;
        IOctopusAsyncRepository repository;
        System.Security.Cryptography.X509Certificates.X509Certificate2 certificate;

        [SetUp]
        public void BeforeEachTest()
        {
            configuration = Substitute.For<IWritableTentacleConfiguration>();
            certificate = new CertificateGenerator(new NullLog()).GenerateNew("CN=Hello");
            configuration.GenerateNewCertificate().Returns(certificate);

            repository = Substitute.For<IOctopusAsyncRepository>();
            var certificateConfiguration = Substitute.For<ICertificateConfigurationRepository>();
            certificateConfiguration.GetOctopusCertificate().Returns(Task.FromResult(new CertificateConfigurationResource { Thumbprint = ServerThumbprint }));
            repository.CertificateConfiguration.Returns(certificateConfiguration);

            var client = Substitute.For<IOctopusAsyncClient>();
            client.Repository.Returns(repository);
            client.ForSystem().Returns(repository);
            var clientInitializer = Substitute.For<IOctopusClientInitializer>();
            clientInitializer.CreateClient(Arg.Any<ApiEndpointOptions>(), Arg.Any<bool>()).Returns(Task.FromResult(client));

            var selector = Substitute.For<IApplicationInstanceSelector>();
            selector.Current.Returns(_ => new ApplicationInstanceConfiguration(null, null!, null!, null!));

            Command = new ReplaceCertificateCommand(
                new Lazy<IWritableTentacleConfiguration>(() => configuration),
                clientInitializer,
                new SpaceRepositoryFactory(),
                Substitute.For<ISystemLog>(),
                selector,
                Substitute.For<ILogFileOnlyLogger>());
        }

        [Test]
        public void ShouldOnlyChangeTheThumbprintOfAPollingWorkerAndTrustTheServer()
        {
            var endpoint = new PollingTentacleEndpointResource { Uri = "poll://abc123/", Thumbprint = "OLD" };
            var worker = new WorkerResource { Id = "Workers-1", Name = "MyWorker", Endpoint = endpoint, WorkerPoolIds = new ReferenceCollection("WorkerPools-1") };
            repository.Workers.FindByName("MyWorker", Arg.Any<CancellationToken>()).Returns(Task.FromResult(worker));

            Start("--server=http://octopus.test", "--apiKey=API-KEY", "--name=MyWorker");

            repository.Workers.Received().Modify(Arg.Is<WorkerResource>(w => w == worker
                && ((PollingTentacleEndpointResource)w.Endpoint).Thumbprint == certificate.Thumbprint
                && ((PollingTentacleEndpointResource)w.Endpoint).Uri == "poll://abc123/"
                && w.WorkerPoolIds.Contains("WorkerPools-1")), Arg.Any<CancellationToken>());
            configuration.Received().AddOrUpdateTrustedOctopusServer(Arg.Is<OctopusServerConfiguration>(s =>
                s.Thumbprint == ServerThumbprint
                && s.CommunicationStyle == CommunicationStyle.TentacleActive
                && s.Address == new Uri("https://octopus.test:10943")
                && s.SubscriptionId == "poll://abc123/"));
        }

        [Test]
        public void ShouldRefuseAnAmbiguousNameRatherThanGuessBetweenATargetAndAWorker()
        {
            repository.Machines.FindByName("Shared", Arg.Any<CancellationToken>()).Returns(Task.FromResult(new MachineResource { Name = "Shared", Endpoint = new ListeningTentacleEndpointResource() }));
            repository.Workers.FindByName("Shared", Arg.Any<CancellationToken>()).Returns(Task.FromResult(new WorkerResource { Name = "Shared", Endpoint = new ListeningTentacleEndpointResource() }));

            Action start = () => Start("--server=http://octopus.test", "--apiKey=API-KEY", "--name=Shared");

            start.Should().Throw<ControlledFailureException>().WithMessage("*--id*");
            configuration.DidNotReceive().GenerateNewCertificate();
        }
    }
}
