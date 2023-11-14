using System;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    [TestFixture]
    public class DisablingScriptServiceV3AlphaTests : IntegrationTest
    {
        [Test]
        [TentacleConfigurations]
        public async Task DisablingScriptServiceV3AlphaViaConfigUsesToScriptServiceV2(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
                    .RecordMethodUsages<IAsyncClientScriptServiceV2>(out var scriptServiceV2Usages)
                    .RecordMethodUsages<IAsyncClientScriptServiceV3Alpha>(out var scriptServiceV3AlphaUsages)
                    .Build())
                .WithClientOptions(options =>
                {
                    options.DisableScriptServiceV3Alpha = true;
                })
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBody(new ScriptBuilder()
                    .Print("Lets do it")
                    .PrintNTimesWithDelay("another one", 10, TimeSpan.FromSeconds(1))
                    .Print("All done"))
                .Build();

            var (finalResponse, logs) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            var allLogs = logs.JoinLogs();

            allLogs.Should().MatchRegex(".*Lets do it\nanother one\nanother one\nanother one\nanother one\nanother one\nanother one\nanother one\nanother one\nanother one\nanother one\nAll done.*");

            // there should be no calls to the Script Service V3 Alpha service.
            scriptServiceV3AlphaUsages.ForAll().Started.Should().Be(0);
            scriptServiceV3AlphaUsages.ForAll().Completed.Should().Be(0);


            //there should be _some_ calls to ScriptServiceV2
            scriptServiceV2Usages.ForAll().Started.Should().NotBe(0);
            scriptServiceV2Usages.ForAll().Completed.Should().NotBe(0);
        }
    }
}