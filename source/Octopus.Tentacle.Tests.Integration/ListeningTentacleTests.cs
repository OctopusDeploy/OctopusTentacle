using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.TentacleClient;

namespace Octopus.Tentacle.Tests.Integration
{
    public class ListeningTentacleTests
    {
        [Test]
        public async Task BasicCommunicationsWithAListeningTentacle()
        {
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            using (var runningTentacle = await new ListeningTentacleBuilder(Support.Certificates.ServerPublicThumbprint)
                       .Build(CancellationToken.None))
            {
                var tentacleClient = new TentacleClientBuilder(octopus)
                    .WithServiceUri(runningTentacle.ServiceUri)
                    .WithRemoteThumbprint(runningTentacle.Thumbprint)
                    .Build(CancellationToken.None);

                var res = tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(new ScriptTicket("1212"), 111));
                res.ExitCode.Should().Be(0);
            }
        }

        [Test]
        public async Task BasicCommunicationsWithAnOldListeningTentacle()
        {
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            using var tmp = new TemporaryDirectory();
            var oldTentacleExe = await TentacleFetcher.GetTentacleVersion(tmp.DirectoryPath, "6.3.451");

            using (var runningTentacle = await new ListeningTentacleBuilder(Support.Certificates.ServerPublicThumbprint)
                       .WithTentacleExe(oldTentacleExe)
                       .Build(CancellationToken.None))
            {
                var tentacleClient = new TentacleClientBuilder(octopus)
                    .WithServiceUri(runningTentacle.ServiceUri)
                    .WithRemoteThumbprint(runningTentacle.Thumbprint)
                    .Build(CancellationToken.None);

                var res = tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(new ScriptTicket("1212"), 111));
                res.ExitCode.Should().Be(0);
            }
        }
    }
}
