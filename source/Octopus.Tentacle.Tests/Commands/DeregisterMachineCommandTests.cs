using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Client;
using Octopus.Client.Model;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Commands.Support;
using Octopus.Tentacle.Tests.Support;

namespace Octopus.Tentacle.Tests.Commands
{
    public class DeregisterMachineCommandTests
    {
        [Test]
        public async Task ShouldNotContinueIfMultipleMatchesButAllowMultipleIsNotSupplied()
        {
            const string expectedThumbPrint1 = "ABCDEFGHIJKLMNOP";
            const string expectedThumbPrint2 = "1234124123412344";

            var configuration = new StubTentacleConfiguration
            {
                TrustedOctopusThumbprints = new List<string> { "NON-MATCHING-THUMBPRINT" },
                TentacleCertificate = new CertificateGenerator(new NullLog()).GenerateNew($"CN={Guid.NewGuid()}")
            };
            var command = CommandBuilder.DeregisterMachine(configuration);
            var asyncRepository = Substitute.For<IOctopusSpaceAsyncRepository>();

            var matchingMachines = new List<MachineResource>
            {
                new MachineResource { Name = "m1", Thumbprint = expectedThumbPrint1 },
                new MachineResource { Name = "m2", Thumbprint = expectedThumbPrint2 }
            };
            asyncRepository.Machines.FindByThumbprint(Arg.Any<string>())
                .ReturnsForAnyArgs(matchingMachines.AsTask());

            Func<Task> exec = () => command.Deregister(asyncRepository);
            await exec.Should().ThrowAsync<ControlledFailureException>().WithMessage(DeregisterMachineCommand.MultipleMatchErrorMsg);
        }

        [Test]
        public async Task ShouldDeregisterIfServerExistsIndependOfTentacleConfiguration()
        {
            const string expectedThumbPrint = "ABCDEFGHIJKLMNOP";
            var configuration = new StubTentacleConfiguration
            {
                TrustedOctopusThumbprints = new List<string> { "NON-MATCHING-THUMBPRINT" },
                TentacleCertificate = new CertificateGenerator(new NullLog()).GenerateNew($"CN={Guid.NewGuid()}")
            };
            var log = Substitute.For<ISystemLog>();
            var command = CommandBuilder.DeregisterMachine(configuration, log);
            var asyncRepository = Substitute.For<IOctopusSpaceAsyncRepository>();

            asyncRepository.Certificates.GetOctopusCertificate()
                .ReturnsForAnyArgs(new CertificateConfigurationResource { Thumbprint = expectedThumbPrint }.AsTask());

            const string machineName = "MachineToBeDeleted";
            var matchingMachines = new List<MachineResource>
            {
                new MachineResource { Name = machineName, Thumbprint = expectedThumbPrint }
            };
            asyncRepository.Machines.FindByThumbprint(Arg.Any<string>())
                .ReturnsForAnyArgs(matchingMachines.AsTask());

            await command.Deregister(asyncRepository);

            log.Received().Info($"Deleting machine '{machineName}' from the Octopus Server...");
        }

        [Test]
        public async Task ShouldDeleteWhenMatchesThumbprint()
        {
            const string expectedThumbPrint = "ABCDEFGHIJKLMNOP";
            var configuration = new StubTentacleConfiguration
            {
                TrustedOctopusThumbprints = new List<string> { expectedThumbPrint },
                TentacleCertificate = new CertificateGenerator(new NullLog()).GenerateNew($"CN={Guid.NewGuid()}")
            };
            var log = Substitute.For<ISystemLog>();
            var command = CommandBuilder.DeregisterMachine(configuration, log);
            var asyncRepository = Substitute.For<IOctopusSpaceAsyncRepository>();

            asyncRepository.Certificates.GetOctopusCertificate()
                .ReturnsForAnyArgs(new CertificateConfigurationResource { Thumbprint = expectedThumbPrint }.AsTask());

            const string machineName = "MachineToBeDeleted";
            var matchingMachines = new List<MachineResource>
            {
                new MachineResource { Name = machineName, Thumbprint = expectedThumbPrint }
            };
            asyncRepository.Machines.FindByThumbprint(Arg.Any<string>())
                .ReturnsForAnyArgs(matchingMachines.AsTask());

            await command.Deregister(asyncRepository);

            log.Received().Info($"Deleting machine '{machineName}' from the Octopus Server...");
            log.Received().Info(DeregisterMachineCommand.DeregistrationSuccessMsg);
        }
    }
}
