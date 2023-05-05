using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using Halibut.ServiceModel;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.TentacleClient;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration
{
    public class PollingTentacleTest
    {
        [Test]
        public async Task BasicCommunicationsWithWithAPollingTentacle()
        {
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);
            
            var cts = new CancellationTokenSource();
            var tentacleId = PollingSubscriptionId.Generate();
            
            var (disposable, runningTentacle) = new PollingTentacleBuilder(port, Support.Certificates.ServerPublicThumbprint)
                .WithTentaclePollSubscription(tentacleId)
                .Build(cts.Token);
            
            using (disposable)
            {
                var testTask = Task.Run(() =>
                {
                    var tentacleClient = new TentacleClientBuilder(octopus)
                        .WithRemoteThumbprint(Support.Certificates.TentaclePublicThumbprint)
                        .WithServiceUri(tentacleId)
                        .Build(CancellationToken.None);

                    var res = tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(new ScriptTicket("1212"), 111));
                    res.ExitCode.Should().Be(0);
                }, cts.Token);
                
                await Task.WhenAny(runningTentacle, testTask);
                cts.Cancel();
                await Task.WhenAll(runningTentacle, testTask);
            }
        }

        [Test]
        public async Task BasicCommunicationsWithWithAnOldPollingTentacle()
        {
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            var cts = new CancellationTokenSource();
            var tentacleId = PollingSubscriptionId.Generate();

            using var tmp = new TemporaryDirectory();
            var oldTentacleExe = await TentacleFetcher.GetTentacleVersion(tmp.DirectoryPath, "6.3.451");

            var (disposable, runningTentacle) = new PollingTentacleBuilder(port, Support.Certificates.ServerPublicThumbprint)
                .WithTentaclePollSubscription(tentacleId)
                .WithTentacleExe(oldTentacleExe)
                .Build(cts.Token);

            using (disposable)
            {
                var testTask = Task.Run(() =>
                {
                    var tentacleClient = new TentacleClientBuilder(octopus)
                        .WithRemoteThumbprint(Support.Certificates.TentaclePublicThumbprint)
                        .WithServiceUri(tentacleId)
                        .Build(CancellationToken.None);

                    var res = tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(new ScriptTicket("1212"), 111));
                    res.ExitCode.Should().Be(0);
                }, cts.Token);

                await Task.WhenAny(runningTentacle, testTask);
                cts.Cancel();
                await Task.WhenAll(runningTentacle, testTask);
            }
        }
    }
}
