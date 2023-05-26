using System;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    [RunTestsInParallelLocallyIfEnabledButNeverOnTeamCity]
    [IntegrationTestTimeout]
    public class ClientScriptExecutorObservesScriptObserverBackoffStrategy : IntegrationTest
    {
        [Test]
        [TestCaseSource(typeof(AllTentacleTypesWithV1V2ScriptServices))]
        public async Task TheScriptObserverBackoffShouldBeRespected(TentacleType tentacleType, string tentacleVersion)
        {
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion(tentacleVersion)
                .WithScriptObserverBackoffStrategy(new FuncScriptObserverBackoffStrategy(iters => TimeSpan.FromSeconds(20)))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .CountCallsToScriptServiceV2(out var scriptServiceV2CallCounts)
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(new ScriptBuilder().PrintNTimesWithDelay("another one", 10, TimeSpan.FromSeconds(1)))
                .Build();

            var (_, logs) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);

            scriptServiceV2CallCounts.GetStatusCallCountStarted.Should().BeLessThan(3);
        }
    }
}