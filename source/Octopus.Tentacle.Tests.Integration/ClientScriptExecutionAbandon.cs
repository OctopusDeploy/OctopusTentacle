using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Client;
using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Logging;
using Octopus.Tentacle.Core.Util;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ClientScriptExecutionAbandon : IntegrationTest
    {
        // PIDs of script processes a test spawned. Captured during the test (while the Tentacle's temp dir
        // still exists) and force-killed in TearDown so nothing leaks onto the CI agent — including the
        // un-killable (kill-disabled) processes, and any process left behind when a test fails early.
        readonly List<int> spawnedProcessPids = new();

        [TearDown]
        public void ForceKillSpawnedProcesses()
        {
            foreach (var pid in spawnedProcessPids)
            {
                try { using var process = Process.GetProcessById(pid); process.Kill(); }
                catch { /* already gone — best effort */ }
            }

            spawnedProcessPids.Clear();
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.Version2)]
        public async Task AbandonScript_WhenProcessCannotBeKilled_LeavesItRunningAndReturnsAbandonedExitCode(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            // TentacleDebugDisableProcessKill_UNSAFE_FOR_PRODUCTION=1 makes Hitman a no-op, so CancelScript
            // cannot terminate the process — it is genuinely stuck. AbandonScript then returns promptly with
            // AbandonedExitCode without waiting, and the un-killable process is left running.
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(x => x.WithRunTentacleEnvironmentVariable(EnvironmentVariables.TentacleDebugDisableProcessKill_UNSAFE_FOR_PRODUCTION, "1"))
                .Build(CancellationToken);

            var startFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "start");
            var releaseFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "release");
            var pidFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "pid");

            var command = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder()
                    .WritePidToFile(pidFile)
                    .CreateFile(startFile)
                    .WaitForFileToExist(releaseFile))
                .WithIsolationLevel(ScriptIsolationLevel.NoIsolation)
                .Build();

            var client = clientTentacle.TentacleClient;
            var log = Log();

            // Explicit StartScript (not ExecuteScript) so we drive cancel and abandon ourselves.
            var startResult = await client.StartScript(command, StartScriptIsBeingReAttempted.FirstAttempt, log, CancellationToken);

            await Wait.For(() => File.Exists(startFile),
                TimeSpan.FromSeconds(30),
                () => throw new Exception("Script did not start"),
                CancellationToken);

            TrackSpawnedProcess(pidFile);

            // Cancel: Hitman is a no-op, so the process keeps running.
            var afterCancel = await client.CancelScript(startResult.ContextForNextCommand, log);

            // Abandon: returns the abandoned terminal state without waiting for the still-running process.
            await client.AbandonScript(command.ScriptTicket, log, CancellationToken);

            var finalResult = await RunStatusUntilComplete(client, afterCancel.ContextForNextCommand, log);
            finalResult.ScriptStatus.ExitCode.Should().Be(ScriptExitCodes.AbandonedExitCode);

            // The process was never killed (kill disabled), so abandon left it running.
            AssertProcessIsRunning(pidFile);

            // Release the still-running script, then complete it so the workspace is cleaned up.
            File.WriteAllText(releaseFile, "");
            await client.CompleteScript(finalResult.ContextForNextCommand, log, CancellationToken);
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.Version2)]
        public async Task AbandonScript_WithNoPriorCancel_KillsTheProcessAndReturnsAbandonedExitCode(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            // Anti-abuse: a direct AbandonScript with no prior CancelScript must still attempt the kill. Kill is
            // NOT disabled here, so the abandon branch's best-effort kill actually terminates the process.
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .Build(CancellationToken);

            var startFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "start");
            var releaseFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "release");
            var pidFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "pid");

            var command = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder()
                    .WritePidToFile(pidFile)
                    .CreateFile(startFile)
                    .WaitForFileToExist(releaseFile))
                .WithIsolationLevel(ScriptIsolationLevel.NoIsolation)
                .Build();

            var client = clientTentacle.TentacleClient;
            var log = Log();

            var startResult = await client.StartScript(command, StartScriptIsBeingReAttempted.FirstAttempt, log, CancellationToken);

            await Wait.For(() => File.Exists(startFile),
                TimeSpan.FromSeconds(30),
                () => throw new Exception("Script did not start"),
                CancellationToken);

            TrackSpawnedProcess(pidFile);

            // Direct abandon, NO prior cancel. We never write releaseFile to allow the script to complete,
            // so it only completes because the abandon branch killed it.
            await client.AbandonScript(command.ScriptTicket, log, CancellationToken);

            var finalResult = await RunStatusUntilComplete(client, startResult.ContextForNextCommand, log);
            finalResult.ScriptStatus.ExitCode.Should().Be(ScriptExitCodes.AbandonedExitCode);

            // Kill was enabled, so abandon's best-effort kill landed and the process is gone.
            AssertProcessExits(pidFile, TimeSpan.FromSeconds(10));

            await client.CompleteScript(finalResult.ContextForNextCommand, log, CancellationToken);
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.Version2)]
        public async Task AbandonScript_ReleasesIsolationMutexEvenWhileProcessIsStillRunning(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            // The whole reason Tentacle needs an abandon RPC is to release the isolation mutex when CancelScript
            // can't unstick the script. A FullIsolation script gets stuck (kill disabled), abandon is called, and
            // a second FullIsolation script with the same mutex name must then be able to acquire the mutex and run.
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(x => x.WithRunTentacleEnvironmentVariable(EnvironmentVariables.TentacleDebugDisableProcessKill_UNSAFE_FOR_PRODUCTION, "1"))
                .Build(CancellationToken);

            var startFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "start");
            var releaseFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "release");
            var pidFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "pid");
            const string sharedMutex = "abandon-test-mutex";

            var firstCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder()
                    .WritePidToFile(pidFile)
                    .CreateFile(startFile)
                    .WaitForFileToExist(releaseFile))
                .WithIsolationLevel(ScriptIsolationLevel.FullIsolation)
                .WithIsolationMutexName(sharedMutex)
                .Build();

            var client = clientTentacle.TentacleClient;
            var log = Log();

            var startResult = await client.StartScript(firstCommand, StartScriptIsBeingReAttempted.FirstAttempt, log, CancellationToken);

            await Wait.For(() => File.Exists(startFile),
                TimeSpan.FromSeconds(30),
                () => throw new Exception("First script did not start"),
                CancellationToken);

            TrackSpawnedProcess(pidFile);

            var afterCancel = await client.CancelScript(startResult.ContextForNextCommand, log);
            await client.AbandonScript(firstCommand.ScriptTicket, log, CancellationToken);

            // Second FullIsolation script with the SAME mutex name. If abandon released the mutex, this script
            // can acquire it and run to completion. Otherwise it blocks behind the still-alive first script.
            var secondStartFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "second-start");
            var secondCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder().CreateFile(secondStartFile))
                .WithIsolationLevel(ScriptIsolationLevel.FullIsolation)
                .WithIsolationMutexName(sharedMutex)
                .Build();

            var (secondResult, _) = await client.ExecuteScript(secondCommand, CancellationToken);
            secondResult.ExitCode.Should().Be(0);
            File.Exists(secondStartFile).Should().BeTrue("second script should have run after the mutex was released");

            // Release the still-running first script, then complete it.
            File.WriteAllText(releaseFile, "");
            var firstFinal = await RunStatusUntilComplete(client, afterCancel.ContextForNextCommand, log);
            await client.CompleteScript(firstFinal.ContextForNextCommand, log, CancellationToken);
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.Version2)]
        public async Task ExecuteScript_WhenCancellationStaysPending_EscalatesToAbandon_AndReleasesMutex(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            // Kill is disabled, so the script is genuinely stuck and CancelScript can't end it. With
            // abandonAfterCancellationPendingFor set short, the orchestrator escalates from cancel to abandon,
            // which releases the isolation mutex. We prove that by the outcome: a second FullIsolation script
            // with the same mutex name can only run once the mutex is released. This is the ExecuteScript
            // (orchestrator) path; the explicit-AbandonScript path is AbandonScript_ReleasesIsolationMutex... above.
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(x => x.WithRunTentacleEnvironmentVariable(EnvironmentVariables.TentacleDebugDisableProcessKill_UNSAFE_FOR_PRODUCTION, "1"))
                .Build(CancellationToken);

            var startFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "start");
            var releaseFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "release");
            var pidFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "pid");
            const string sharedMutex = "escalation-test-mutex";

            var stuckCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder()
                    .WritePidToFile(pidFile)
                    .CreateFile(startFile)
                    .WaitForFileToExist(releaseFile))
                .WithIsolationLevel(ScriptIsolationLevel.FullIsolation)
                .WithIsolationMutexName(sharedMutex)
                .Build();

            var client = clientTentacle.TentacleClient;

            using var executionCts = new System.Threading.CancellationTokenSource();
            var stuckExecution = Task.Run(async () => await client.ExecuteScript(
                stuckCommand,
                _ => { },
                _ => Task.CompletedTask,
                Log(),
                executionCts.Token,
                abandonAfterCancellationPendingFor: TimeSpan.FromSeconds(2)));

            await Wait.For(() => File.Exists(startFile),
                TimeSpan.FromSeconds(30),
                () => throw new Exception("Script did not start"),
                CancellationToken);

            TrackSpawnedProcess(pidFile);

            // Cancel stays pending (kill disabled), so after the threshold the orchestrator escalates to abandon.
            executionCts.Cancel();
            await FluentActions.Invoking(async () => await stuckExecution).Should().ThrowAsync<OperationCanceledException>();

            // Outcome: a second FullIsolation script with the same mutex now runs — only possible if abandon released it.
            var secondStartFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "second-start");
            var secondCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder().CreateFile(secondStartFile))
                .WithIsolationLevel(ScriptIsolationLevel.FullIsolation)
                .WithIsolationMutexName(sharedMutex)
                .Build();

            var (secondResult, _) = await client.ExecuteScript(secondCommand, CancellationToken);
            secondResult.ExitCode.Should().Be(0);
            File.Exists(secondStartFile).Should().BeTrue("escalation to abandon should have released the mutex");

            // Cleanup: release the still-alive first script.
            File.WriteAllText(releaseFile, "");
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.Version1)]
        public async Task ExecuteScript_OnV1Tentacle_DoesNotEscalateToAbandon(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            // A V1 Tentacle has no abandon verb. If the orchestrator wrongly escalated, the V1 executor's
            // AbandonScript throws NotSupportedException. Asserting the cancelled run throws OperationCanceledException
            // (and therefore NOT NotSupportedException) proves the gate held and we never escalated.
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(x => x.WithRunTentacleEnvironmentVariable(EnvironmentVariables.TentacleDebugDisableProcessKill_UNSAFE_FOR_PRODUCTION, "1"))
                .Build(CancellationToken);

            var startFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "start");
            var releaseFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "release");
            var pidFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "pid");

            var command = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder()
                    .WritePidToFile(pidFile)
                    .CreateFile(startFile)
                    .WaitForFileToExist(releaseFile))
                .WithIsolationLevel(ScriptIsolationLevel.NoIsolation)
                .Build();

            var client = clientTentacle.TentacleClient;

            using var executionCts = new System.Threading.CancellationTokenSource();
            var execution = Task.Run(async () => await client.ExecuteScript(
                command,
                _ => { },
                _ => Task.CompletedTask,
                Log(),
                executionCts.Token,
                abandonAfterCancellationPendingFor: TimeSpan.FromSeconds(1)));

            await Wait.For(() => File.Exists(startFile),
                TimeSpan.FromSeconds(30),
                () => throw new Exception("Script did not start"),
                CancellationToken);

            TrackSpawnedProcess(pidFile);

            executionCts.Cancel();

            // Well past the 1s threshold: if the V1 gate were broken the orchestrator would have escalated to
            // AbandonScript (which throws NotSupportedException) by now.
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Release so the stuck script can finish and the run can unwind.
            File.WriteAllText(releaseFile, "");

            await FluentActions.Invoking(async () => await execution).Should().ThrowAsync<OperationCanceledException>();
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.Version2)]
        public async Task AbandonScript_WhenAScriptIsWaitingOnAMutex_AndIsCancelled_ReturnsCancelledNotAbandoned(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            // End-to-end counterpart to ScriptServiceV2Fixture.AbandonScript_WhileWaitingOnTheIsolationMutex_ExitsCancelled.
            // A script still waiting on the isolation mutex never started a process, so when it is cancelled (and
            // abandoned) it exits cancelled, not abandoned. Abandon has nothing to act on for a queued script.
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .Build(CancellationToken);

            var holderStartFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "holder-start");
            var holderReleaseFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "holder-release");
            var holderPidFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "holder-pid");
            var waiterRanFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "waiter-ran");
            const string sharedMutex = "queued-cancel-test-mutex";

            // Holder takes the mutex and holds it (it doesn't need to be stuck), so the waiter must queue behind it.
            var holderCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder()
                    .WritePidToFile(holderPidFile)
                    .CreateFile(holderStartFile)
                    .WaitForFileToExist(holderReleaseFile))
                .WithIsolationLevel(ScriptIsolationLevel.FullIsolation)
                .WithIsolationMutexName(sharedMutex)
                .Build();

            var client = clientTentacle.TentacleClient;
            var log = Log();

            var holderStart = await client.StartScript(holderCommand, StartScriptIsBeingReAttempted.FirstAttempt, log, CancellationToken);

            await Wait.For(() => File.Exists(holderStartFile),
                TimeSpan.FromSeconds(30),
                () => throw new Exception("Holder script did not start"),
                CancellationToken);

            TrackSpawnedProcess(holderPidFile);

            // Waiter wants the same mutex, so it blocks acquiring it — its process never starts.
            var waiterCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder().CreateFile(waiterRanFile))
                .WithIsolationLevel(ScriptIsolationLevel.FullIsolation)
                .WithIsolationMutexName(sharedMutex)
                .Build();

            var waiterStart = await client.StartScript(waiterCommand, StartScriptIsBeingReAttempted.FirstAttempt, log, CancellationToken);

            // Give the waiter time to reach the mutex queue, then cancel and abandon it.
            await Task.Delay(TimeSpan.FromSeconds(2));
            var waiterAfterCancel = await client.CancelScript(waiterStart.ContextForNextCommand, log);
            await client.AbandonScript(waiterCommand.ScriptTicket, log, CancellationToken);

            var waiterFinal = await RunStatusUntilComplete(client, waiterAfterCancel.ContextForNextCommand, log);
            waiterFinal.ScriptStatus.ExitCode.Should().Be(ScriptExitCodes.CanceledExitCode);
            File.Exists(waiterRanFile).Should().BeFalse("a script blocked on the isolation mutex never starts its process");

            await client.CompleteScript(waiterFinal.ContextForNextCommand, log, CancellationToken);

            // Cleanup: release the holder, then complete it.
            File.WriteAllText(holderReleaseFile, "");
            var holderFinal = await RunStatusUntilComplete(client, holderStart.ContextForNextCommand, log);
            await client.CompleteScript(holderFinal.ContextForNextCommand, log, CancellationToken);
        }

        void TrackSpawnedProcess(string pidFile)
        {
            if (File.Exists(pidFile) && int.TryParse(File.ReadAllText(pidFile).Trim(), out var pid) && pid > 0)
                spawnedProcessPids.Add(pid);
        }

        static ITentacleClientTaskLog Log()
            => new SerilogLoggerBuilder().Build().ForContext<TentacleClient>().ToITentacleTaskLog();

        async Task<ScriptOperationExecutionResult> RunStatusUntilComplete(ITentacleClient client, CommandContext context, ITentacleClientTaskLog log)
        {
            var result = await client.GetStatus(context, log, CancellationToken);
            while (result.ScriptStatus.State != ProcessState.Complete)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200));
                result = await client.GetStatus(result.ContextForNextCommand, log, CancellationToken);
            }

            return result;
        }

        static void AssertProcessIsRunning(string pidFile)
        {
            File.Exists(pidFile).Should().BeTrue($"the script should have written its PID to '{pidFile}'");
            int.TryParse(File.ReadAllText(pidFile).Trim(), out var pid).Should().BeTrue("a valid PID should have been written");
            IsProcessRunning(pid).Should().BeTrue($"abandon leaves an un-killable process running, so PID {pid} should still be alive");
        }

        static void AssertProcessExits(string pidFile, TimeSpan timeout)
        {
            File.Exists(pidFile).Should().BeTrue($"the script should have written its PID to '{pidFile}'");
            int.TryParse(File.ReadAllText(pidFile).Trim(), out var pid).Should().BeTrue("a valid PID should have been written");

            // abandon's best-effort kill is asynchronous, so give it a moment to actually terminate.
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline && IsProcessRunning(pid))
                System.Threading.Thread.Sleep(100);

            IsProcessRunning(pid).Should().BeFalse($"abandon best-effort-kills a killable process, so PID {pid} should be gone");
        }

        static bool IsProcessRunning(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                return !process.HasExited;
            }
            catch (ArgumentException) { return false; }       // not running
            catch (InvalidOperationException) { return false; } // exited
        }
    }
}
