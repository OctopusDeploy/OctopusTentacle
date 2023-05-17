using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;

namespace Octopus.Tentacle.Tests.Integration
{
    public class CapabilitiesServiceV2Test
    {
        [TestCase(true, null)] // The version of tentacle compiled from the current code.
        [TestCase(false, "5.0.4")] // First linux Release 9/9/2019
        [TestCase(false, "5.0.12")] // The autofac service was in octopus shared.
        [TestCase(false, "6.3.451")] // the autofac service is in tentacle, but tentacle does not have the capabilities service.
        public async Task CapabilitiesFromAnOlderTentacleWhichHasNoCapabilitiesService_WorksWithTheBackwardsCompatabilityDecorator(
            bool useTentacleBuiltFromCurrentCode,
            string version)
        {
            var token = TestCancellationToken.Token();
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithLegacyContractSupport()
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            using var tmp = new TemporaryDirectory();
            var oldTentacleExe = useTentacleBuiltFromCurrentCode ? TentacleExeFinder.FindTentacleExe() : await TentacleFetcher.GetTentacleVersion(tmp.DirectoryPath, version);

            using var runningTentacle = await new PollingTentacleBuilder(port, Support.Certificates.ServerPublicThumbprint)
                .WithTentacleExe(oldTentacleExe)
                .Build(token);

            var tentacleClient = new LegacyTentacleClientBuilder(octopus)
                .ForRunningTentacle(runningTentacle)
                .Build(token);

            var capabilities = tentacleClient.CapabilitiesServiceV2.GetCapabilities().SupportedCapabilities;

            capabilities.Should().Contain("IScriptService");
            capabilities.Should().Contain("IFileTransferService");
            if (useTentacleBuiltFromCurrentCode)
            {
                capabilities.Should().Contain("IScriptServiceV2Alpha");
                capabilities.Count.Should().Be(3);
            }
            else
            {
                capabilities.Count.Should().Be(2);
            }

        }
    }
}
