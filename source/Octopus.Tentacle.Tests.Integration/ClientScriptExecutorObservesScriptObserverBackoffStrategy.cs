using System;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    public class ClientScriptExecutorObservesScriptObserverBackoffStrategy
    {
        [Test]
        public async Task TheScriptObserverBackoffShouldBeRespected()
        {
            var token = TestCancellationToken.Token();
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            using (var runningTentacle = await new PollingTentacleBuilder(port, Support.Certificates.ServerPublicThumbprint)
                       .Build(token))
            {
                var serviceEndPoint = new ServiceEndPoint(runningTentacle.ServiceUri, runningTentacle.Thumbprint);

                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(new ScriptBuilder().PrintNTimesWithDelay("another one", 10, TimeSpan.FromSeconds(1)))
                    .Build();

                CountingCallsScriptServiceV2Decorator? callCounts = null;
                var tentacleServicesDecorator = new TentacleServiceDecoratorBuilder()
                    .DecorateScriptServiceV2With(inner => callCounts = new CountingCallsScriptServiceV2Decorator(inner))
                    .Build();

                var tentacleClient = new TentacleClient(serviceEndPoint, 
                    octopus, 
                    new FuncScriptObserverBackoffStrategy(iters => TimeSpan.FromSeconds(20)), 
                    tentacleServicesDecorator, 
                    TimeSpan.FromMinutes(4));
                var (_, logs) = await tentacleClient.ExecuteScript(startScriptCommand, token);
                
                
                callCounts.GetStatusCallCountStarted.Should().BeLessThan(3);
            }
        }
    }
}