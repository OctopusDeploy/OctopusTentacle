using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Client.Model.Endpoints;
using Octopus.Tentacle.Certificates;
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
        public void ShouldUpdateEveryRegistrationWithTheOldThumbprintInEverySpace()
        {
            // Arrange
            using var tentacle = new TentacleConfigurationBuilder().WithCertificateThumbprint("OLD").Build();
            var target = new MachineResource { Id = "Machines-1", Endpoint = new ListeningTentacleEndpointResource { Thumbprint = "OLD" } };
            var worker = new WorkerResource { Id = "Workers-1", Endpoint = new PollingTentacleEndpointResource { Uri = "poll://abc123/", Thumbprint = "OLD" } };
            var octopus = new FakeOctopus();
            var spaceA = octopus.Space("Spaces-1").WithMachine(target);
            var spaceB = octopus.Space("Spaces-2").WithWorker(worker);

            // Act
            ReplaceCertificate(tentacle, octopus);

            // Assert
            var newThumbprint = tentacle.Configuration.TentacleCertificate!.Thumbprint;
            newThumbprint.Should().NotBe("OLD");
            ((TentacleEndpointResource)target.Endpoint).Thumbprint.Should().Be(newThumbprint);
            ((PollingTentacleEndpointResource)worker.Endpoint).Thumbprint.Should().Be(newThumbprint);
            ((PollingTentacleEndpointResource)worker.Endpoint).Uri.Should().Be("poll://abc123/");
            spaceA.Machines.Received().Modify(target, Arg.Any<CancellationToken>());
            spaceB.Workers.Received().Modify(worker, Arg.Any<CancellationToken>());
        }

        [Test]
        public void WhenAnUpdateFailsRerunningWithKeepCertificateFinishesWithTheSameCertificate()
        {
            // Arrange
            using var tentacle = new TentacleConfigurationBuilder().WithCertificateThumbprint("OLD").Build();
            var first = new MachineResource { Id = "Machines-1", Endpoint = new ListeningTentacleEndpointResource { Thumbprint = "OLD" } };
            var second = new MachineResource { Id = "Machines-2", Endpoint = new ListeningTentacleEndpointResource { Thumbprint = "OLD" } };
            var octopus = new FakeOctopus();
            var space = octopus.Space("Spaces-1").WithMachine(first).WithMachine(second);
            var octopusIsDown = true;
            space.Machines.Modify(second, Arg.Any<CancellationToken>()).Returns(_ =>
            {
                if (!octopusIsDown)
                    return Task.FromResult(second);
                octopusIsDown = false;
                ((TentacleEndpointResource)second.Endpoint).Thumbprint = "OLD";
                throw new Exception("Octopus is down");
            });
            Action run = () => ReplaceCertificate(tentacle, octopus);
            run.Should().Throw<Exception>().WithMessage("Octopus is down");
            var savedThumbprint = tentacle.Configuration.TentacleCertificate!.Thumbprint;

            // Act
            ReplaceCertificate(tentacle, octopus, "--keep-certificate-and-update-thumbprint=OLD");

            // Assert
            tentacle.Configuration.TentacleCertificate!.Thumbprint.Should().Be(savedThumbprint);
            ((TentacleEndpointResource)first.Endpoint).Thumbprint.Should().Be(savedThumbprint);
            ((TentacleEndpointResource)second.Endpoint).Thumbprint.Should().Be(savedThumbprint);
        }

        [Test]
        public void WhenNothingMatchesTheCertificateIsNotReplaced()
        {
            // Arrange
            using var tentacle = new TentacleConfigurationBuilder().WithCertificateThumbprint("OLD").Build();
            var octopus = new FakeOctopus();
            octopus.Space("Spaces-1");

            // Act
            Action run = () => ReplaceCertificate(tentacle, octopus);

            // Assert
            run.Should().Throw<ControlledFailureException>().WithMessage("*OLD*");
            tentacle.Configuration.TentacleCertificateThumbprint.Should().Be("OLD");
        }

        static void ReplaceCertificate(TemporaryTentacleConfiguration tentacle, FakeOctopus octopus, params string[] args)
            => new ReplaceCertificateCommand(
                    new Lazy<IWritableTentacleConfiguration>(() => tentacle.Configuration),
                    new Lazy<ICertificateGenerator>(() => new CertificateGenerator(new NullLog())),
                    octopus.ClientInitializer,
                    Substitute.For<IProxyConfigParser>(),
                    Substitute.For<ISystemLog>(),
                    tentacle.Selector,
                    Substitute.For<ILogFileOnlyLogger>())
                .Start(new[] { "--server=http://octopus.test", "--apiKey=API-KEY" }.Concat(args).ToArray(), Substitute.For<ICommandRuntime>(), new OptionSet());

        class FakeOctopus
        {
            readonly IOctopusAsyncClient client = Substitute.For<IOctopusAsyncClient>();
            readonly List<SpaceResource> spaces = new();

            public FakeOctopus()
            {
                var user = new UserResource();
                client.ForSystem().Users.GetCurrent().Returns(user);
                client.ForSystem().Users.GetSpaces(user).Returns(_ => spaces.ToArray());
                ClientInitializer.CreateClient(Arg.Any<ApiEndpointOptions>(), Arg.Any<System.Net.IWebProxy?>()).Returns(client);
            }

            public IOctopusClientInitializer ClientInitializer { get; } = Substitute.For<IOctopusClientInitializer>();

            public FakeSpace Space(string id)
            {
                var space = new SpaceResource { Id = id };
                spaces.Add(space);
                var repository = Substitute.For<IOctopusSpaceAsyncRepository>();
                client.ForSpace(space).Returns(repository);
                return new FakeSpace(repository);
            }
        }

        class FakeSpace
        {
            readonly IOctopusSpaceAsyncRepository repository;
            readonly List<MachineResource> machines = new();
            readonly List<WorkerResource> workers = new();

            public FakeSpace(IOctopusSpaceAsyncRepository repository)
            {
                this.repository = repository;
                repository.Machines.FindByThumbprint(Arg.Any<string>()).Returns(c => machines.FindAll(m => ((TentacleEndpointResource)m.Endpoint).Thumbprint == c.Arg<string>()));
                repository.Workers.FindByThumbprint(Arg.Any<string>()).Returns(c => workers.FindAll(w => ((TentacleEndpointResource)w.Endpoint).Thumbprint == c.Arg<string>()));
            }

            public Octopus.Client.Repositories.Async.IMachineRepository Machines => repository.Machines;
            public Octopus.Client.Repositories.Async.IWorkerRepository Workers => repository.Workers;

            public FakeSpace WithMachine(MachineResource machine)
            {
                machines.Add(machine);
                return this;
            }

            public FakeSpace WithWorker(WorkerResource worker)
            {
                workers.Add(worker);
                return this;
            }
        }
    }
}
