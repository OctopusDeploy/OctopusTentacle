using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.Transport;
using NUnit.Framework;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Support;

namespace Octopus.Tentacle.Tests.Integration
{
    public class JustTryingToGetAPollingTentacle
    {
        [Test]
        [Obsolete("Obsolete")]
        public async Task Doit()
        {
            using(IHalibutRuntime octopus = new HalibutRuntime(Support.Certificates.Server))
            {
                var port = octopus.Listen();

                string tentacleId = "poll://eze";
                var (disposable, runningTentacle) = new PollingTentacleBuilder().DoStuff(port, Support.Certificates.ServerPublicThumbprint, tentacleId);
                using (disposable)
                {
                    var testTask = Task.Run(() =>
                    {
                        var tentacleClient = new TentacleClientBuilder(octopus)
                            .WithRemoteThumbprint(Support.Certificates.TentaclePublicThumbprint)
                            .WithServiceUri(new Uri(tentacleId))
                            .Build(CancellationToken.None);

                        var res = tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(new ScriptTicket("1212"), 111));

                    });
                    await Task.WhenAny(runningTentacle, testTask);

                    await Task.WhenAll(runningTentacle, testTask);
                }
            }
        }
        
        
    }
}