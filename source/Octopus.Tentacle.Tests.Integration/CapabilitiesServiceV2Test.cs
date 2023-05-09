using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.TentacleClient;

namespace Octopus.Tentacle.Tests.Integration
{
    public class CapabilitiesServiceV2Test
    {
        [TestCase(null)] // Current version
        [TestCase("5.0.4")] // First linux Release 9/9/2019
        [TestCase("5.0.12")] // The autofac service was in octopus shared.
        [TestCase("6.3.451")] // the autofac service is in tentacle, but tentacle does not have the capabilities service.
        public async Task CapabilitiesFromAnOlderTentacleWhichHasNoCapabilitiesService_WorksWithTheBackwardsCompatabilityDecorator(string version)
        {
            var cts = new CancellationTokenSource((int)TimeSpan.FromSeconds(120).TotalMilliseconds).Token;
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            using var tmp = new TemporaryDirectory();
            var oldTentacleExe = version == null ? TentacleExeFinder.FindTentacleExe() : await TentacleFetcher.GetTentacleVersion(tmp.DirectoryPath, version);

            using (var runningTentacle = await new PollingTentacleBuilder(port, Support.Certificates.ServerPublicThumbprint)
                       .WithTentacleExe(oldTentacleExe)
                       .Build(cts))
            {
                var tentacleClient = new TentacleClientBuilder(octopus)
                    .ForRunningTentacle(runningTentacle)
                    .Build(cts);

                var capabilities = tentacleClient.CapabilitiesServiceV2.GetCapabilities().SupportedCapabilities;

                capabilities.Should().Contain("IScriptService");
                capabilities.Should().Contain("IFileTransferService");
                capabilities.Count.Should().Be(2);
            }
        }
    }
}