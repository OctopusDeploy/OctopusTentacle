using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;
using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ScriptServiceV2IntegrationTest : IntegrationTest
    {
        [Test]
        [TentacleConfigurations]
        public async Task CanRunScript(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder().CountCallsToScriptServiceV2(out var scriptServiceV2CallCounts).Build())
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

            scriptServiceV2CallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceV2CallCounts.GetStatusCallCountStarted.Should().BeGreaterThan(2);
            scriptServiceV2CallCounts.GetStatusCallCountStarted.Should().BeLessThan(30);
            Logger.Debug("{S}", scriptServiceV2CallCounts.GetStatusCallCountStarted);
            scriptServiceV2CallCounts.CompleteScriptCallCountStarted.Should().Be(1);
            scriptServiceV2CallCounts.CancelScriptCallCountStarted.Should().Be(0);
        }

        [Test]
        [TentacleConfigurations]
        public async Task DelayInStartScriptSavesNetworkCalls(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder().CountCallsToScriptServiceV2(out var scriptServiceV2CallCounts).Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBody(new ScriptBuilder()
                    .Print("Lets do it")
                    .PrintNTimesWithDelay("another one", 10, TimeSpan.FromSeconds(1))
                    .Print("All done"))
                .WithDurationStartScriptCanWaitForScriptToFinish(TimeSpan.FromMinutes(1))
                .Build();

            var (finalResponse, logs) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            var allLogs = logs.JoinLogs();

            allLogs.Should().MatchRegex(".*Lets do it\nanother one\nanother one\nanother one\nanother one\nanother one\nanother one\nanother one\nanother one\nanother one\nanother one\nAll done.*");

            scriptServiceV2CallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceV2CallCounts.GetStatusCallCountStarted.Should().Be(0, "Since start script should wait for the script to finish so we don't need to call get status");
            scriptServiceV2CallCounts.CompleteScriptCallCountStarted.Should().Be(1);
            scriptServiceV2CallCounts.CancelScriptCallCountStarted.Should().Be(0);
        }

        [Test]
        [TentacleConfigurations]
        public async Task WhenTentacleRestartsWhileRunningAScript_TheExitCodeShouldBe_UnknownResultExitCode(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder().CountCallsToScriptServiceV2(out var scriptServiceV2CallCounts).Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .Sleep(TimeSpan.FromSeconds(1))
                    .Print("waitingtobestopped")
                    .Sleep(TimeSpan.FromSeconds(100)))
                .Build();

            var semaphoreSlim = new SemaphoreSlim(0, 1);

            var executingScript = Task.Run(async () =>
                await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, onScriptStatusResponseReceived =>
                {
                    if (onScriptStatusResponseReceived.Logs.JoinLogs().Contains("waitingtobestopped"))
                    {
                        semaphoreSlim.Release();
                    }
                }));

            await semaphoreSlim.WaitAsync(CancellationToken);

            Logger.Information("Stopping and starting tentacle now.");
            await clientTentacle.RunningTentacle.Restart(CancellationToken);

            var (finalResponse, logs) = await executingScript;

            finalResponse.Should().NotBeNull();
            logs.JoinLogs().Should().Contain("waitingtobestopped");
            finalResponse.State.Should().Be(ProcessState.Complete); // This is technically a lie, the process is still running on linux
            finalResponse.ExitCode.Should().Be(ScriptExitCodes.UnknownResultExitCode);

            scriptServiceV2CallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceV2CallCounts.GetStatusCallCountStarted.Should().BeGreaterThan(1);
            scriptServiceV2CallCounts.CompleteScriptCallCountStarted.Should().Be(1);
            scriptServiceV2CallCounts.CancelScriptCallCountStarted.Should().Be(0);
        }

        [Test]
        [TentacleConfigurations]
        public async Task WhenALongRunningScriptIsCancelled_TheScriptShouldStop(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder().CountCallsToScriptServiceV2(out var scriptServiceV2CallCounts).Build())
                .Build(CancellationToken);

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBody(new ScriptBuilder()
                    .Print("hello")
                    .Sleep(TimeSpan.FromSeconds(1))
                    .Print("waitingtobestopped")
                    .Sleep(TimeSpan.FromSeconds(100)))
                .Build();

            var scriptCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
            var stopWatch = Stopwatch.StartNew();
            Exception? actualException = null;

            try
            {
                await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, scriptCancellationTokenSource.Token, onScriptStatusResponseReceived =>
                {
                    if (onScriptStatusResponseReceived.Logs.JoinLogs().Contains("waitingtobestopped"))
                    {
                        scriptCancellationTokenSource.Cancel();
                    }
                });
            }
            catch (Exception ex)
            {
                actualException = ex;
            }

            stopWatch.Stop();

            actualException.Should().NotBeNull().And.BeOfType<OperationCanceledException>().And.Match<Exception>(x => x.Message == "Script execution was cancelled");
            stopWatch.Elapsed.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(10));

            scriptServiceV2CallCounts.StartScriptCallCountStarted.Should().Be(1);
            scriptServiceV2CallCounts.GetStatusCallCountStarted.Should().BeGreaterThan(1);
            scriptServiceV2CallCounts.CancelScriptCallCountStarted.Should().BeGreaterThan(0);
            scriptServiceV2CallCounts.CompleteScriptCallCountStarted.Should().Be(1);
        }
    }
}
