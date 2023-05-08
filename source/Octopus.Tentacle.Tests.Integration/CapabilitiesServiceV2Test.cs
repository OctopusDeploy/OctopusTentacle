using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using NUnit.Framework.Internal;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.TentacleClient;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration
{
    public class CapabilitiesServiceV2Test
    {
        [Test]
        public async Task CapabilitiesAreReturnedFromTheService()
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

                    var capabilities = tentacleClient.CapabilitiesServiceV2.GetCapabilities().SupportedCapabilities;
                    
                    capabilities.Should().Contain("IScriptService");
                    capabilities.Should().Contain("IFileTransferService");
                    capabilities.Count.Should().Be(2);
                    
                }, cts.Token);
                
                await Task.WhenAny(runningTentacle, testTask);
                cts.Cancel();
                await Task.WhenAll(runningTentacle, testTask);
            }
        }

        [TestCase("5.0.12")] // The autofac service was in octopus shared.
        [TestCase("6.3.451")] // the autofac service is in tentacle, but tentacle does not have the capabilities service.
        public async Task CapabilitiesFromAnOlderTentacleWhichHasNoCapabilitiesService_WorksWithTheBackwardsCompatabilityDecorator(string version)
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
            
            var oldTentacleExe = await TentacleFetcher.GetTentacleVersion(tmp.DirectoryPath, version);
            
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

                    var capabilities = tentacleClient.CapabilitiesServiceV2.GetCapabilities().SupportedCapabilities;
                    
                    capabilities.Should().Contain("IScriptService");
                    capabilities.Should().Contain("IFileTransferService");
                    capabilities.Count.Should().Be(2);
                    
                }, cts.Token);
                
                await Task.WhenAny(runningTentacle, testTask);
                cts.Cancel();
                await Task.WhenAll(runningTentacle, testTask);
            }
        }
        
        
    }
}