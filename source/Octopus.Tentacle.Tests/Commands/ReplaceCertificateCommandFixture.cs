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
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Internals.Options;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Support;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class ReplaceCertificateCommandFixture
    {
        const string ServerThumbprint = "SERVERTHUMBPRINT";

        [Test]
        public void ShouldOnlyChangeTheThumbprintOfAPollingWorkerAndTrustTheServer()
        {
            using var tentacle = new TentacleConfigurationBuilder().Build();
            var worker = PollingWorker("MyWorker", "poll://abc123/", "WorkerPools-1");
            var octopus = new FakeOctopus(ServerThumbprint).WithWorker(worker);

            Run(CreateCommand(tentacle, octopus), "--server=http://octopus.test", "--apiKey=API-KEY", "--name=MyWorker");

            var endpoint = (PollingTentacleEndpointResource)worker.Endpoint;
            var newThumbprint = tentacle.Configuration.TentacleCertificate!.Thumbprint;
            newThumbprint.Should().NotBe("OLD");
            endpoint.Thumbprint.Should().Be(newThumbprint);
            endpoint.Uri.Should().Be("poll://abc123/");
            worker.WorkerPoolIds.Should().Contain("WorkerPools-1");

            var server = tentacle.Configuration.TrustedOctopusServers.Should().ContainSingle().Subject;
            server.Thumbprint.Should().Be(ServerThumbprint);
            server.CommunicationStyle.Should().Be(CommunicationStyle.TentacleActive);
            server.Address.Should().Be(new Uri("https://octopus.test:10943"));
            server.SubscriptionId.Should().Be("poll://abc123/");
        }

        [Test]
        public void ShouldRefuseAnAmbiguousNameRatherThanGuessBetweenATargetAndAWorker()
        {
            using var tentacle = new TentacleConfigurationBuilder().Build();
            var octopus = new FakeOctopus(ServerThumbprint)
                .WithMachine(new MachineResource { Name = "Shared", Endpoint = new ListeningTentacleEndpointResource() })
                .WithWorker(new WorkerResource { Name = "Shared", Endpoint = new ListeningTentacleEndpointResource() });

            Action run = () => Run(CreateCommand(tentacle, octopus), "--server=http://octopus.test", "--apiKey=API-KEY", "--name=Shared");

            run.Should().Throw<ControlledFailureException>().WithMessage("*--id*");
            tentacle.Configuration.TentacleCertificate.Should().BeNull();
        }

        static WorkerResource PollingWorker(string name, string subscription, string pool)
            => new()
            {
                Id = "Workers-1",
                Name = name,
                Endpoint = new PollingTentacleEndpointResource { Uri = subscription, Thumbprint = "OLD" },
                WorkerPoolIds = new ReferenceCollection(pool)
            };

        static ReplaceCertificateCommand CreateCommand(TemporaryTentacleConfiguration tentacle, FakeOctopus octopus)
            => new(
                new Lazy<IWritableTentacleConfiguration>(() => tentacle.Configuration),
                octopus.ClientInitializer,
                new SpaceRepositoryFactory(),
                Substitute.For<ISystemLog>(),
                tentacle.Selector,
                Substitute.For<ILogFileOnlyLogger>());

        static void Run(ReplaceCertificateCommand command, params string[] args)
            => command.Start(args, Substitute.For<ICommandRuntime>(), new OptionSet());

        class FakeOctopus
        {
            readonly IOctopusAsyncRepository repository = Substitute.For<IOctopusAsyncRepository>();

            public FakeOctopus(string serverThumbprint)
            {
                var certificateConfiguration = Substitute.For<ICertificateConfigurationRepository>();
                certificateConfiguration.GetOctopusCertificate().Returns(Task.FromResult(new CertificateConfigurationResource { Thumbprint = serverThumbprint }));
                repository.CertificateConfiguration.Returns(certificateConfiguration);

                var client = Substitute.For<IOctopusAsyncClient>();
                client.Repository.Returns(repository);
                client.ForSystem().Returns(repository);
                ClientInitializer.CreateClient(Arg.Any<ApiEndpointOptions>(), Arg.Any<bool>()).Returns(Task.FromResult(client));
            }

            public IOctopusClientInitializer ClientInitializer { get; } = Substitute.For<IOctopusClientInitializer>();

            public FakeOctopus WithWorker(WorkerResource worker)
            {
                repository.Workers.FindByName(worker.Name, Arg.Any<CancellationToken>()).Returns(Task.FromResult(worker));
                return this;
            }

            public FakeOctopus WithMachine(MachineResource machine)
            {
                repository.Machines.FindByName(machine.Name, Arg.Any<CancellationToken>()).Returns(Task.FromResult(machine));
                return this;
            }
        }
    }
}
