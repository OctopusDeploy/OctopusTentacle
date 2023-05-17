using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Integration.Support;

namespace Octopus.Tentacle.Tests.Integration
{
    public class PollingTentacleTest
    {
        [Test]
        public async Task BasicCommunicationsWithWithAPollingTentacle()
        {
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithLegacyContractSupport()
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            using var runningTentacle = await new PollingTentacleBuilder(port, Support.Certificates.ServerPublicThumbprint)
                .Build(CancellationToken.None);

            var tentacleClient = new LegacyTentacleClientBuilder(octopus)
                .ForRunningTentacle(runningTentacle)
                .Build(CancellationToken.None);

            var res = tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(new ScriptTicket("1212"), 111));
            res.ExitCode.Should().Be(0);
        }

        [Test]
        public async Task BasicCommunicationsWithWithAnOldPollingTentacle()
        {
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithLegacyContractSupport()
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            using var tmp = new TemporaryDirectory();
            var oldTentacleExe = await TentacleFetcher.GetTentacleVersion(tmp.DirectoryPath, "6.3.451");

            using var runningTentacle = await new PollingTentacleBuilder(port, Support.Certificates.ServerPublicThumbprint)
                .WithTentacleExe(oldTentacleExe)
                .Build(CancellationToken.None);

            var tentacleClient = new LegacyTentacleClientBuilder(octopus)
                .ForRunningTentacle(runningTentacle)
                .Build(CancellationToken.None);

            var res = tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(new ScriptTicket("1212"), 111));
            res.ExitCode.Should().Be(0);
        }
    }
}
