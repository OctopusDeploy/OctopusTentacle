using System;
using System.Collections;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    [RunTestsInParallelLocallyIfEnabledButNeverOnTeamCity]
    // [IntegrationTestTimeout]
    public class ClientScriptExecutionAdditionalScripts : IntegrationTest
    {
        public class AllTentacleTypesWithV1AndV2ScriptServiceTentacles : IEnumerable
        {
            public IEnumerator GetEnumerator()
            {
                return CartesianProduct.Of(new TentacleTypesToTest(), new V1OnlyAndV2ScriptServiceTentacleVersions()).GetEnumerator();
            }
        }

        [Test]
        [TestCaseSource(typeof(AllTentacleTypesWithV1AndV2ScriptServiceTentacles))]
        public async Task AdditionalScriptsWork(TentacleType tentacleType, string tentacleVersion)
        {
            using var clientTentacle = await new ClientAndTentacleBuilder(tentacleType)
                .WithTentacleVersion(tentacleVersion)
                .WithScriptObserverBackoffStrategy(new FuncScriptObserverBackoffStrategy(iters => TimeSpan.FromSeconds(20)))
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .CountCallsToScriptServiceV2(out var scriptServiceV2CallCounts)
                    .Build())
                .Build(CancellationToken);

            var scriptBuilder = new ScriptBuilder().Print("Hello");
            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithAdditionalScriptTypes(ScriptType.Bash, scriptBuilder.BuildBashScript())
                // Additional Scripts don't actually work on tentacle for anything other than bash.
                // Below is what we would have expected to tentacle to work with.
                //.WithAdditionalScriptTypes(ScriptType.PowerShell, scriptBuilder.BuildPowershellScript())
                // But instead we need to send the powershell in the scriptbody.
                .WithScriptBody(scriptBuilder.BuildPowershellScript())
                .Build();

            var (finalResponse, logs) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            var allLogs = logs.JoinLogs();

            allLogs.Should().Contain("Hello");
        }
    }
}