using System;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators;
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
            var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithScriptObserverBackoffStrategy(new FuncScriptObserverBackoffStrategy(iters => TimeSpan.FromSeconds(20)))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages(tentacleConfigurationTestCase, out var recordedUsages)
                    .Build())
                .Build(CancellationToken);

            var startScriptCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder().PrintNTimesWithDelay("another one", 10, TimeSpan.FromSeconds(1)))
                .Build();

            var (_, logs) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);

            var maxGetStatusCalls = tentacleConfigurationTestCase.ScriptServiceToTest == TentacleConfigurationTestCases.ScriptServiceV1Type
                ? 4 // When using ScriptServiceV1 we check the final status twice on completion
                : 3;

            recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Started
                .Should()
                .BeGreaterThan(0)
                .And
                .BeLessThan(maxGetStatusCalls);
        }
    }
}
