using System;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ClientScriptExecutorObservesScriptObserverBackoffStrategy : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(testCommonVersions: true)]
        public async Task TheScriptObserverBackoffShouldBeRespected(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithScriptObserverBackoffStrategy(new FuncScriptObserverBackoffStrategy(iters => TimeSpan.FromSeconds(20)))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var tracingStats)
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder().PrintNTimesWithDelay("another one", 10, TimeSpan.FromSeconds(1)))
                .Build();

            var (_, logs) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);

            tracingStats.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Started
                .Should()
                .BeGreaterThan(0)
                .And
                .BeLessThan(3);
        }
    }
}
