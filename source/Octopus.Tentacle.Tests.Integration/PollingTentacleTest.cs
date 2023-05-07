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

            using (var runningTentacle = new PollingTentacleBuilder(port, Support.Certificates.ServerPublicThumbprint)
                       .Build(cts.Token))
            {
                var testTask = Task.Run(() =>
                {
                    var tentacleClient = new TentacleClientBuilder(octopus)
                        .WithRemoteThumbprint(Support.Certificates.TentaclePublicThumbprint)
                        .WithServiceUri(runningTentacle.ServiceUri)
                        .Build(CancellationToken.None);

                    var res = tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(new ScriptTicket("1212"), 111));
                    res.ExitCode.Should().Be(0);
                }, cts.Token);
                
                await Task.WhenAny(runningTentacle.Task, testTask);
                cts.Cancel();
                await Task.WhenAll(runningTentacle.Task, testTask);
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

            using var tmp = new TemporaryDirectory();
            var oldTentacleExe = await TentacleFetcher.GetTentacleVersion(tmp.DirectoryPath, "6.3.451");

            using (var runningTentacle = new PollingTentacleBuilder(port, Support.Certificates.ServerPublicThumbprint)
                       .WithTentacleExe(oldTentacleExe)
                       .Build(cts.Token))
            {
                var testTask = Task.Run(() =>
                {
                    var tentacleClient = new TentacleClientBuilder(octopus)
                        .WithRemoteThumbprint(Support.Certificates.TentaclePublicThumbprint)
                        .WithServiceUri(runningTentacle.ServiceUri)
                        .Build(CancellationToken.None);

                    var res = tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(new ScriptTicket("1212"), 111));
                    res.ExitCode.Should().Be(0);
                }, cts.Token);

                await Task.WhenAny(runningTentacle.Task, testTask);
                cts.Cancel();
                await Task.WhenAll(runningTentacle.Task, testTask);
            }
        }
    }
}
