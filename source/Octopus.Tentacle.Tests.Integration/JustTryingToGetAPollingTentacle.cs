using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Integration.Support;

namespace Octopus.Tentacle.Tests.Integration
{
    public class JustTryingToGetAPollingTentacle
    {
        [Test]
        public async Task Doit()
        {
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);
            var cts = new CancellationTokenSource();

            string tentacleId = "poll://eze";
            var (disposable, runningTentacle) = new PollingTentacleBuilder().DoStuff(port, Support.Certificates.ServerPublicThumbprint, tentacleId, cts.Token);
            using (disposable)
            {
                var testTask = Task.Run(() =>
                {
                    var tentacleClient = new TentacleClientBuilder(octopus)
                        .WithRemoteThumbprint(Support.Certificates.TentaclePublicThumbprint)
                        .WithServiceUri(new Uri(tentacleId))
                        .Build(CancellationToken.None);

                    var res = tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(new ScriptTicket("1212"), 111));
                }, cts.Token);
                
                await Task.WhenAny(runningTentacle, testTask);
                cts.Cancel();
                await Task.WhenAll(runningTentacle, testTask);
            }
        }
    }
}
