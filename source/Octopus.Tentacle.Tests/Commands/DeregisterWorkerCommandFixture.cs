using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Diagnostics;
using Octopus.Shared;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Security;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Tests.Support;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class DeregisterWorkerCommandFixture : CommandFixture<DeregisterWorkerCommand>
    {
        ISystemLog log;
        IProxyConfigParser proxyConfig;
        IOctopusAsyncRepository asyncRepository;
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            asyncRepository = Substitute.For<IOctopusAsyncRepository>();
            proxyConfig = Substitute.For<IProxyConfigParser>();
            log = Substitute.For<ISystemLog>();
        }

        [Test]
        public void ShouldNotContinueIfMultipleMatchesButAllowMultipleIsNotSupplied()
        {
            const string expectedThumbPrint1 = "ABCDEFGHIJKLMNOP";
            const string expectedThumbPrint2 = "1234124123412344";

            var configuration = new StubTentacleConfiguration
            {
                TrustedOctopusThumbprints = new List<string> { "NON-MATCHING-THUMBPRINT" },
                TentacleCertificate = new CertificateGenerator().GenerateNew($"CN={Guid.NewGuid()}", new Shared.Diagnostics.NullLog())
            };
            Command = new DeregisterWorkerCommand(new Lazy<ITentacleConfiguration>(() => configuration),
                log,
                Substitute.For<IApplicationInstanceSelector>(),
                proxyConfig,
                Substitute.For<IOctopusClientInitializer>(),
                new SpaceRepositoryFactory());

            var matchingMachines = new List<WorkerResource>
            {
                new WorkerResource { Name = "m1", Thumbprint = expectedThumbPrint1 },
                new WorkerResource { Name = "m2", Thumbprint = expectedThumbPrint2 }
            };
            asyncRepository.Workers.FindByThumbprint(Arg.Any<string>())
                .ReturnsForAnyArgs(matchingMachines.AsTask());

            Func<Task> exec = () => Command.Deregister(asyncRepository);
            exec.ShouldThrow<ControlledFailureException>().WithMessage(DeregisterWorkerCommand.MultipleMatchErrorMsg);
        }

        [Test]
        public async Task ShouldDeregisterIfServerExistsIndependOfTentacleConfiguration()
        {
            const string expectedThumbPrint = "ABCDEFGHIJKLMNOP";
            var configuration = new StubTentacleConfiguration
            {
                TrustedOctopusThumbprints = new List<string> { "NON-MATCHING-THUMBPRINT" },
                TentacleCertificate = new CertificateGenerator().GenerateNew($"CN={Guid.NewGuid()}", new Shared.Diagnostics.NullLog())
            };

            Command = new DeregisterWorkerCommand(new Lazy<ITentacleConfiguration>(() => configuration),
                log,
                Substitute.For<IApplicationInstanceSelector>(),
                proxyConfig,
                Substitute.For<IOctopusClientInitializer>(),
                new SpaceRepositoryFactory());

            const string machineName = "MachineToBeDeleted";
            var matchingMachines = new List<WorkerResource>
            {
                new WorkerResource { Name = machineName, Thumbprint = expectedThumbPrint }
            };
            asyncRepository.Workers.FindByThumbprint(Arg.Any<string>())
                .ReturnsForAnyArgs(matchingMachines.AsTask());

            await Command.Deregister(asyncRepository);

            log.Received().Info($"Deleting worker '{machineName}' from the Octopus Server...");
        }

        [Test]
        public async Task ShouldDeleteWhenMatchesThumbprint()
        {
            const string expectedThumbPrint = "ABCDEFGHIJKLMNOP";
            var configuration = new StubTentacleConfiguration
            {
                TrustedOctopusThumbprints = new List<string> { expectedThumbPrint },
                TentacleCertificate = new CertificateGenerator().GenerateNew($"CN={Guid.NewGuid()}", new Shared.Diagnostics.NullLog())
            };

            Command = new DeregisterWorkerCommand(new Lazy<ITentacleConfiguration>(() => configuration),
                log,
                Substitute.For<IApplicationInstanceSelector>(),
                proxyConfig,
                Substitute.For<IOctopusClientInitializer>(),
                new SpaceRepositoryFactory());

            asyncRepository.CertificateConfiguration.GetOctopusCertificate()
                .ReturnsForAnyArgs(new CertificateConfigurationResource { Thumbprint = expectedThumbPrint }.AsTask());

            const string machineName = "MachineToBeDeleted";
            var matchingMachines = new List<WorkerResource>
            {
                new WorkerResource { Name = machineName, Thumbprint = expectedThumbPrint }
            };
            asyncRepository.Workers.FindByThumbprint(Arg.Any<string>())
                .ReturnsForAnyArgs(matchingMachines.AsTask());

            await Command.Deregister(asyncRepository);

            log.Received().Info($"Deleting worker '{machineName}' from the Octopus Server...");
            log.Received().Info(DeregisterWorkerCommand.DeregistrationSuccessMsg);
        }
    }
}