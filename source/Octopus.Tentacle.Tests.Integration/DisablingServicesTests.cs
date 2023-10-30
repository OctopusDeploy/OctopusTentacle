using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class DisablingServicesTests : IntegrationTest
    {
        [Test]
        [TentacleConfigurations]
        public async Task DisablingScriptServiceV2ViaConfigUsesToScriptServiceV1(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder().CountCallsToScriptServiceV2(out var scriptServiceV2CallCounts).Build())
                .WithTentacleClientOptions(builder =>
                {
                    builder.DisableService(nameof(IScriptServiceV2));
                })
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV2Builder()
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

            // there should be no calls to the Script Service V2 service.
            scriptServiceV2CallCounts.Should().BeEquivalentTo(new ScriptServiceV2CallCounts());
        }
    }
}
