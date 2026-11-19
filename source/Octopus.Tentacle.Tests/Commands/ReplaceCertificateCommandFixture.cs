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
        [Test]
        public void ShouldSaveTheNewThumbprintOnTheWorkerFoundByName()
        {
            // Arrange
            using var tentacle = new TentacleConfigurationBuilder().Build();
            var worker = PollingWorker("MyWorker", "poll://abc123/");
            var octopus = new FakeOctopus().WithWorker(worker);

            // Act
            ReplaceCertificate(tentacle, octopus, "--name=MyWorker");

            // Assert
            var newThumbprint = tentacle.Configuration.TentacleCertificate!.Thumbprint;
            newThumbprint.Should().NotBe("OLD");
            worker.Endpoint.Should().BeOfType<PollingTentacleEndpointResource>().Which.Thumbprint.Should().Be(newThumbprint);
            octopus.Workers.Received().Modify(worker, Arg.Any<CancellationToken>());
        }

        [Test]
        public void ShouldSaveTheNewThumbprintOnTheWorkerFoundById()
        {
            // Arrange
            using var tentacle = new TentacleConfigurationBuilder().Build();
            var worker = PollingWorker("MyWorker", "poll://abc123/");
            var octopus = new FakeOctopus().WithWorker(worker);

            // Act
            ReplaceCertificate(tentacle, octopus, "--id=Workers-1");

            // Assert
            ((PollingTentacleEndpointResource)worker.Endpoint).Thumbprint.Should().Be(tentacle.Configuration.TentacleCertificate!.Thumbprint);
            octopus.Workers.Received().Modify(worker, Arg.Any<CancellationToken>());
        }

        [Test]
        public void WhenPollingTheSubscriptionIdDoesNotChange()
        {
            // Arrange
            using var tentacle = new TentacleConfigurationBuilder().Build();
            var worker = PollingWorker("MyWorker", "poll://abc123/");
            var octopus = new FakeOctopus().WithWorker(worker);

            // Act
            ReplaceCertificate(tentacle, octopus, "--name=MyWorker");

            // Assert
            ((PollingTentacleEndpointResource)worker.Endpoint).Uri.Should().Be("poll://abc123/");
        }

        [Test]
        public void ShouldRefuseAnAmbiguousNameRatherThanGuessBetweenATargetAndAWorker()
        {
            // Arrange
            using var tentacle = new TentacleConfigurationBuilder().Build();
            var octopus = new FakeOctopus()
                .WithMachine(new MachineResource { Name = "Shared", Endpoint = new ListeningTentacleEndpointResource() })
                .WithWorker(new WorkerResource { Name = "Shared", Endpoint = new ListeningTentacleEndpointResource() });

            // Act
            Action run = () => ReplaceCertificate(tentacle, octopus, "--name=Shared");

            // Assert
            run.Should().Throw<ControlledFailureException>().WithMessage("*--id*");
            tentacle.Configuration.TentacleCertificate.Should().BeNull();
        }

        static WorkerResource PollingWorker(string name, string subscription)
            => new()
            {
                Id = "Workers-1",
                Name = name,
                Endpoint = new PollingTentacleEndpointResource { Uri = subscription, Thumbprint = "OLD" }
            };

        static ReplaceCertificateCommand CreateCommand(TemporaryTentacleConfiguration tentacle, FakeOctopus octopus)
            => new(
                new Lazy<IWritableTentacleConfiguration>(() => tentacle.Configuration),
                octopus.ClientInitializer,
                new SpaceRepositoryFactory(),
                Substitute.For<ISystemLog>(),
                tentacle.Selector,
                Substitute.For<ILogFileOnlyLogger>());

        static void ReplaceCertificate(TemporaryTentacleConfiguration tentacle, FakeOctopus octopus, string machineSelector)
            => CreateCommand(tentacle, octopus).Start(
                new[] { "--server=http://octopus.test", "--apiKey=API-KEY", machineSelector },
                Substitute.For<ICommandRuntime>(),
                new OptionSet());

        class FakeOctopus
        {
            readonly IOctopusAsyncRepository repository = Substitute.For<IOctopusAsyncRepository>();

            public FakeOctopus()
            {
                var client = Substitute.For<IOctopusAsyncClient>();
                client.Repository.Returns(repository);
                ClientInitializer.CreateClient(Arg.Any<ApiEndpointOptions>(), Arg.Any<bool>()).Returns(Task.FromResult(client));
            }

            public IOctopusClientInitializer ClientInitializer { get; } = Substitute.For<IOctopusClientInitializer>();

            public IWorkerRepository Workers => repository.Workers;

            public FakeOctopus WithWorker(WorkerResource worker)
            {
                repository.Workers.FindByName(worker.Name, Arg.Any<CancellationToken>()).Returns(Task.FromResult(worker));
                repository.Workers.Get(worker.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult(worker));
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
