// using System;
// using System.Diagnostics;
// using System.Threading;
// using System.Threading.Tasks;
// using FluentAssertions;
// using NUnit.Framework;
// using Octopus.Tentacle.CommonTestUtils.Builders;
// using Octopus.Tentacle.Contracts;
// using Octopus.Tentacle.Contracts.ClientServices;
// using Octopus.Tentacle.Tests.Integration.Common.Builders.Decorators;
// using Octopus.Tentacle.Tests.Integration.Support;
// using Octopus.Tentacle.Tests.Integration.Util;
// using Octopus.Tentacle.Tests.Integration.Util.Builders;
// using Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators;
//
// namespace Octopus.Tentacle.Tests.Integration
// {
//     [IntegrationTestTimeout]
//     public class ScriptServiceV2IntegrationTest : IntegrationTest
//     {
//         [Test]
//         [TentacleConfigurations]
//         public async Task CanRunScript(TentacleConfigurationTestCase tentacleConfigurationTestCase)
//         {
//             await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
//                 .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
//                     .RecordMethodUsages<IAsyncClientScriptServiceV2>(out var methodUsages)
//                     .Build())
//                 .Build(CancellationToken);
//
//             var startScriptCommand = new TestExecuteShellScriptCommandBuilder()
//                 .SetScriptBody(new ScriptBuilder()
//                     .Print("Lets do it")
//                     .PrintNTimesWithDelay("another one", 10, TimeSpan.FromSeconds(1))
//                     .Print("All done"))
//                 .Build();
//
//             var (finalResponse, logs) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);
//
//             finalResponse.State.Should().Be(ProcessState.Complete);
//             finalResponse.ExitCode.Should().Be(0);
//
//             var allLogs = logs.JoinLogs();
//
//             allLogs.Should().MatchRegex(".*Lets do it\nanother one\nanother one\nanother one\nanother one\nanother one\nanother one\nanother one\nanother one\nanother one\nanother one\nAll done.*");
//
//             methodUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(1);
//             methodUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Started.Should().BeGreaterThan(2).And.BeLessThan(30);
//             methodUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().Be(1);
//             methodUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).Started.Should().Be(0);
//         }
//
//         [Test]
//         [TentacleConfigurations]
//         public async Task DelayInStartScriptSavesNetworkCalls(TentacleConfigurationTestCase tentacleConfigurationTestCase)
//         {
//             await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
//                 .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
//                     .RecordMethodUsages<IAsyncClientScriptServiceV2>(out var recordedUsages)
//                     .Build())
//                 .Build(CancellationToken);
//
//             var startScriptCommand = new TestExecuteShellScriptCommandBuilder()
//                 .SetScriptBody(new ScriptBuilder()
//                     .Print("Lets do it")
//                     .PrintNTimesWithDelay("another one", 10, TimeSpan.FromSeconds(1))
//                     .Print("All done"))
//                 .WithDurationStartScriptCanWaitForScriptToFinish(TimeSpan.FromMinutes(1))
//                 .Build();
//
//             var (finalResponse, logs) = await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken);
//
//             finalResponse.State.Should().Be(ProcessState.Complete);
//             finalResponse.ExitCode.Should().Be(0);
//
//             var allLogs = logs.JoinLogs();
//
//             allLogs.Should().MatchRegex(".*Lets do it\nanother one\nanother one\nanother one\nanother one\nanother one\nanother one\nanother one\nanother one\nanother one\nanother one\nAll done.*");
//
//             recordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(1);
//             recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Started.Should().Be(0, "Since start script should wait for the script to finish so we don't need to call get status");
//             recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().Be(1);
//             recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).Started.Should().Be(0);
//         }
//
//         [Test]
//         [TentacleConfigurations]
//         public async Task WhenTentacleRestartsWhileRunningAScript_TheExitCodeShouldBe_UnknownResultExitCode(TentacleConfigurationTestCase tentacleConfigurationTestCase)
//         {
//             await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
//                 .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
//                     .RecordMethodUsages<IAsyncClientScriptServiceV2>(out var recordedUsages)
//                     .Build())
//                 .Build(CancellationToken);
//
//             var startScriptCommand = new TestExecuteShellScriptCommandBuilder()
//                 .SetScriptBody(new ScriptBuilder()
//                     .Print("hello")
//                     .Sleep(TimeSpan.FromSeconds(1))
//                     .Print("waitingtobestopped")
//                     .Sleep(TimeSpan.FromSeconds(100)))
//                 .Build();
//
//             var semaphoreSlim = new SemaphoreSlim(0, 1);
//
//             var executingScript = Task.Run(async () =>
//                 await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, CancellationToken, onScriptStatusResponseReceived =>
//                 {
//                     if (onScriptStatusResponseReceived.Logs.JoinLogs().Contains("waitingtobestopped"))
//                     {
//                         semaphoreSlim.Release();
//                     }
//                 }));
//
//             await semaphoreSlim.WaitAsync(CancellationToken);
//
//             Logger.Information("Stopping and starting tentacle now.");
//             await clientTentacle.RunningTentacle.Restart(CancellationToken);
//
//             var (finalResponse, logs) = await executingScript;
//
//             finalResponse.Should().NotBeNull();
//             logs.JoinLogs().Should().Contain("waitingtobestopped");
//             finalResponse.State.Should().Be(ProcessState.Complete); // This is technically a lie, the process is still running on linux
//             finalResponse.ExitCode.Should().Be(ScriptExitCodes.UnknownResultExitCode);
//
//             recordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(1);
//             recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Started.Should().BeGreaterThan(1);
//             recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().Be(1);
//             recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).Started.Should().Be(0);
//         }
//
//         [Test]
//         [TentacleConfigurations]
//         public async Task WhenALongRunningScriptIsCancelled_TheScriptShouldStop(TentacleConfigurationTestCase tentacleConfigurationTestCase)
//         {
//             await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
//                 .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
//                     .RecordMethodUsages<IAsyncClientScriptServiceV2>(out var recordedUsages)
//                     .Build())
//                 .Build(CancellationToken);
//
//             var startScriptCommand = new TestExecuteShellScriptCommandBuilder()
//                 .SetScriptBody(new ScriptBuilder()
//                     .Print("hello")
//                     .Sleep(TimeSpan.FromSeconds(1))
//                     .Print("waitingtobestopped")
//                     .Sleep(TimeSpan.FromSeconds(100)))
//                 .Build();
//
//             var scriptCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
//             var stopWatch = Stopwatch.StartNew();
//             Exception? actualException = null;
//
//             try
//             {
//                 await clientTentacle.TentacleClient.ExecuteScript(startScriptCommand, scriptCancellationTokenSource.Token, onScriptStatusResponseReceived =>
//                 {
//                     if (onScriptStatusResponseReceived.Logs.JoinLogs().Contains("waitingtobestopped"))
//                     {
//                         scriptCancellationTokenSource.Cancel();
//                     }
//                 });
//             }
//             catch (Exception ex)
//             {
//                 actualException = ex;
//             }
//
//             stopWatch.Stop();
//
//             actualException.Should().NotBeNull().And.BeOfType<OperationCanceledException>().And.Match<Exception>(x => x.Message == "Script execution was cancelled");
//             stopWatch.Elapsed.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(10));
//
//             recordedUsages.For(nameof(IAsyncClientScriptServiceV2.StartScriptAsync)).Started.Should().Be(1);
//             recordedUsages.For(nameof(IAsyncClientScriptServiceV2.GetStatusAsync)).Started.Should().BeGreaterThan(1);
//             recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CompleteScriptAsync)).Started.Should().Be(1);
//             recordedUsages.For(nameof(IAsyncClientScriptServiceV2.CancelScriptAsync)).Started.Should().BeGreaterThanOrEqualTo(1);
//         }
//
//         [Test]
//         [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.Version2)]
//         public async Task WhenOnCompleteTakesLongerThan_OnCancellationAbandonCompleteScriptAfter_AndTheExecutionIsNotCancelled_TheOrchestratorWaitsForCleanUpToComplete(TentacleConfigurationTestCase tentacleConfigurationTestCase)
//         {
//             bool calledWithNonCancelledCT = false;
//
//             await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
//                 .WithTentacleServiceDecorator(new TentacleServiceDecoratorBuilder()
//                     .RecordMethodUsages(tentacleConfigurationTestCase, out var recordedUsages)
//                     .DecorateScriptServiceV2With(b => b
//                         .BeforeCompleteScript(async (_, _, halibutProxyRequestOptions) =>
//                         {
//                             await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken);
//                             calledWithNonCancelledCT = !halibutProxyRequestOptions.RequestCancellationToken.IsCancellationRequested;
//                         })
//                         .Build())
//                     .Build())
//                 .Build(CancellationToken);
//
//             var scriptCommand = new TestExecuteShellScriptCommandBuilder()
//                 .SetScriptBody(b => b.Print("Hello"))
//                 .Build();
//
//             var tentacleClient = clientTentacle.TentacleClient;
//
//             tentacleClient.OnCancellationAbandonCompleteScriptAfter = TimeSpan.FromMilliseconds(1);
//
//             await tentacleClient.ExecuteScript(scriptCommand, CancellationToken);
//
//
//             calledWithNonCancelledCT.Should().Be(true);
//         }
//     }
// }
