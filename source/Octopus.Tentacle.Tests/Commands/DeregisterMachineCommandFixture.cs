﻿using System;
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
    public class DeregisterMachineCommandFixture : CommandFixture<DeregisterMachineCommand>
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
            Command = new DeregisterMachineCommand(new Lazy<ITentacleConfiguration>(() => configuration), 
                                                   log, 
                                                   Substitute.For<IApplicationInstanceSelector>(), 
                                                   proxyConfig,
                                                   Substitute.For<IOctopusClientInitializer>());

            var matchingMachines = new List<MachineResource>
            {
                new MachineResource { Name = "m1", Thumbprint = expectedThumbPrint1 },
                new MachineResource { Name = "m2", Thumbprint = expectedThumbPrint2 }
            };
            asyncRepository.Machines.FindByThumbprint(Arg.Any<string>())
                .ReturnsForAnyArgs(matchingMachines.AsTask());

            Func<Task> exec = () => Command.Deregister(asyncRepository);
            exec.ShouldThrow<ControlledFailureException>().WithMessage(DeregisterMachineCommand.MultipleMatchErrorMsg);
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

            Command = new DeregisterMachineCommand(new Lazy<ITentacleConfiguration>(() => configuration),
                                                   log, 
                                                   Substitute.For<IApplicationInstanceSelector>(),
                                                   proxyConfig,
                                                   Substitute.For<IOctopusClientInitializer>());

            const string machineName = "MachineToBeDeleted";
            var matchingMachines = new List<MachineResource>
            {
                new MachineResource { Name = machineName, Thumbprint = expectedThumbPrint }
            };
            asyncRepository.Machines.FindByThumbprint(Arg.Any<string>())
                .ReturnsForAnyArgs(matchingMachines.AsTask());

            await Command.Deregister(asyncRepository);

            log.Received().Info($"Deleting machine '{machineName}' from the Octopus server...");
            log.Received().Error(DeregisterMachineCommand.ThumbprintNotFoundMsg);
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

            Command = new DeregisterMachineCommand(new Lazy<ITentacleConfiguration>(() => configuration),
                                                   log,
                                                   Substitute.For<IApplicationInstanceSelector>(),
                                                   proxyConfig,
                                                   Substitute.For<IOctopusClientInitializer>());

            asyncRepository.CertificateConfiguration.GetOctopusCertificate()
                .ReturnsForAnyArgs(new CertificateConfigurationResource { Thumbprint = expectedThumbPrint }.AsTask());

            const string machineName = "MachineToBeDeleted";
            var matchingMachines = new List<MachineResource>
            {
                new MachineResource { Name = machineName, Thumbprint = expectedThumbPrint }
            };
            asyncRepository.Machines.FindByThumbprint(Arg.Any<string>())
                .ReturnsForAnyArgs(matchingMachines.AsTask());

            await Command.Deregister(asyncRepository);

            log.Received().Info($"Deleting entry '{expectedThumbPrint}' in tentacle.config");
            log.Received().Info($"Deleting machine '{machineName}' from the Octopus server...");
            log.Received().Info(DeregisterMachineCommand.DeregistrationSuccessMsg);
        }
    }
}
