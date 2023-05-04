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

            string tentacleId = "poll://ggez";
            var (disposable, runningTentacle) = new PollingTentacleBuilder().Build(port, Support.Certificates.ServerPublicThumbprint, tentacleId, cts.Token);
            using (disposable)
            {
                var testTask = Task.Run(() =>
                {
                    var tentacleClient = new TentacleClientBuilder(octopus)
                        .WithRemoteThumbprint(Support.Certificates.TentaclePublicThumbprint)
                        .WithServiceUri(new Uri(tentacleId))
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
        [Obsolete("Obsolete")]
        public void Halibut()
        {
            var services = new DelegateServiceFactory();
            services.Register<IFoo>(() => new Foo());
            using (var octopus = new HalibutRuntime(Support.Certificates.Server))
            using (var tentaclePolling = new HalibutRuntime(services, Support.Certificates.Tentacle))
            {
                var octopusPort = octopus.Listen();
                octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), Support.Certificates.ServerPublicThumbprint));

                var svc = octopus.CreateClient<IFoo>("poll://SQ-TENTAPOLL", Support.Certificates.TentaclePublicThumbprint);
                svc.FooBar();
            }
        }
        
        public interface IFoo
        {
            public void FooBar();
        }

        public class Foo : IFoo
        {
            public void FooBar()
            {
                
            }
        }
    }
}
