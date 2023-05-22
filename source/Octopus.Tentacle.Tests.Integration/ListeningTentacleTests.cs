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
        public async Task BasicCommunicationsWithWithAPollingTentacle()
        {
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            //var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            using (var runningTentacle = await new ListeningTentacleBuilder(Support.Certificates.ServerPublicThumbprint)
                       .Build(CancellationToken.None))
            {
                var tentacleClient = new TentacleClientBuilder(octopus)
                    .WithServiceUri(new Uri($"https://localhost:{runningTentacle.Port}"))
                    .WithRemoteThumbprint(runningTentacle.Thumbprint)
                    .Build(CancellationToken.None);

                var res = tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(new ScriptTicket("1212"), 111));
                res.ExitCode.Should().Be(0);
            }
        }
    }
}
