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
using Octopus.Shared.Security;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Tests.Support;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class DeregisterWorkerMachineCommandFixture : CommandFixture<DeregisterWorkerMachineCommand>
    {
        ILog log;
        IProxyConfigParser proxyConfig;
        IOctopusAsyncRepository asyncRepository;
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            asyncRepository = Substitute.For<IOctopusAsyncRepository>();
            proxyConfig = Substitute.For<IProxyConfigParser>();
            log = Substitute.For<ILog>();
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
            Command = new DeregisterWorkerMachineCommand(new Lazy<ITentacleConfiguration>(() => configuration), 
                log, 
                Substitute.For<IApplicationInstanceSelector>(), 
                proxyConfig,
                Substitute.For<IOctopusClientInitializer>());

            var matchingMachines = new List<WorkerMachineResource>
            {
                new WorkerMachineResource { Name = "m1", Thumbprint = expectedThumbPrint1 },
                new WorkerMachineResource { Name = "m2", Thumbprint = expectedThumbPrint2 }
            };
            asyncRepository.WorkerMachines.FindByThumbprint(Arg.Any<string>())
                .ReturnsForAnyArgs(matchingMachines.AsTask());

            Func<Task> exec = () => Command.Deregister(asyncRepository);
            exec.ShouldThrow<ControlledFailureException>().WithMessage(DeregisterWorkerMachineCommand.MultipleMatchErrorMsg);
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

            Command = new DeregisterWorkerMachineCommand(new Lazy<ITentacleConfiguration>(() => configuration),
                log, 
                Substitute.For<IApplicationInstanceSelector>(),
                proxyConfig,
                Substitute.For<IOctopusClientInitializer>());

            const string machineName = "MachineToBeDeleted";
            var matchingMachines = new List<WorkerMachineResource>
            {
                new WorkerMachineResource { Name = machineName, Thumbprint = expectedThumbPrint }
            };
            asyncRepository.WorkerMachines.FindByThumbprint(Arg.Any<string>())
                .ReturnsForAnyArgs(matchingMachines.AsTask());

            await Command.Deregister(asyncRepository);

            log.Received().Info($"Deleting worker machine '{machineName}' from the Octopus server...");
            log.Received().Error(DeregisterWorkerMachineCommand.ThumbprintNotFoundMsg);
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

            Command = new DeregisterWorkerMachineCommand(new Lazy<ITentacleConfiguration>(() => configuration),
                log,
                Substitute.For<IApplicationInstanceSelector>(),
                proxyConfig,
                Substitute.For<IOctopusClientInitializer>());

            asyncRepository.CertificateConfiguration.GetOctopusCertificate()
                .ReturnsForAnyArgs(new CertificateConfigurationResource { Thumbprint = expectedThumbPrint }.AsTask());

            const string machineName = "MachineToBeDeleted";
            var matchingMachines = new List<WorkerMachineResource>
            {
                new WorkerMachineResource { Name = machineName, Thumbprint = expectedThumbPrint }
            };
            asyncRepository.WorkerMachines.FindByThumbprint(Arg.Any<string>())
                .ReturnsForAnyArgs(matchingMachines.AsTask());

            await Command.Deregister(asyncRepository);

            log.Received().Info($"Deleting entry '{expectedThumbPrint}' in tentacle.config");
            log.Received().Info($"Deleting worker machine '{machineName}' from the Octopus server...");
            log.Received().Info(DeregisterWorkerMachineCommand.DeregistrationSuccessMsg);
        }
    }
}