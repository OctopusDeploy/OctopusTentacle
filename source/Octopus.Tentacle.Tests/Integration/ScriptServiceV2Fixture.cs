using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Core.Services.Scripts;
using Octopus.Tentacle.Core.Services.Scripts.Locking;
using Octopus.Tentacle.Core.Services.Scripts.Logging;
using Octopus.Tentacle.Core.Services.Scripts.Security.Masking;
using Octopus.Tentacle.Core.Services.Scripts.Shell;
using Octopus.Tentacle.Core.Services.Scripts.StateStore;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Services.Scripts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration
{
    [TestFixture]
    public class ScriptServiceV2Fixture
    {
        [Test]
        public async Task ShouldExecuteAScriptSuccessfully()
        {
            var builder = new ScriptServiceV2Builder();
            var service = builder.Build();

            var windowsScript = "& ping.exe localhost -n 1";
            var bashScript = "ping localhost -c 1";

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                .WithIsolation(ScriptIsolationLevel.NoIsolation)
                .WithDurationStartScriptCanWaitForScriptToFinish(null)
                .Build();

            var startScriptResponse = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);
            var (logs, finalResponse) = await RunUntilScriptCompletes(service, startScriptCommand, startScriptResponse);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);
            logs.Count.Should().BeGreaterThan(1);
        }

        [Test]
        public async Task ShouldReturnANonZeroExitCodeForAFailingScript()
        {
            var builder = new ScriptServiceV2Builder();
            var service = builder.Build();

            var windowsScript = "& ping.exe nope -n 1";
            var bashScript = "ping nope -c 1";

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                .WithIsolation(ScriptIsolationLevel.NoIsolation)
                .WithDurationStartScriptCanWaitForScriptToFinish(null)
                .Build();

            var startScriptResponse = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);
            var (logs, finalResponse) = await RunUntilScriptCompletes(service, startScriptCommand, startScriptResponse);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().NotBe(0);
            logs.Count.Should().BeGreaterThan(1);
        }

        [Test]
        public async Task ShouldExecuteMultipleScriptsConcurrently()
        {
            var builder = new ScriptServiceV2Builder();
            var service = builder.Build();

            var bashScript = "sleep 10";
            var windowsScript = "Start-Sleep -Seconds 10";

            var scripts = Enumerable.Range(0, 5).Select(x =>
                new StartScriptCommandAndResponse(command: new StartScriptCommandV2Builder()
                    .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                    .WithIsolation(ScriptIsolationLevel.NoIsolation)
                    .WithDurationStartScriptCanWaitForScriptToFinish(null)
                    .Build())).ToList();

            var started = Stopwatch.StartNew();

            var tasks = new ConcurrentBag<Task>();

            Parallel.ForEach(scripts, script =>
            {
                var task = Task.Run(async () => script.Response = await service.StartScriptAsync(script.Command, CancellationToken.None));
                tasks.Add(task);
            });

            await Task.WhenAll(tasks);

            var startDuration = started.Elapsed;
            startDuration.Should().BeLessThan(TimeSpan.FromSeconds(5));

            await Task.Delay(TimeSpan.FromSeconds(10), CancellationToken.None);

            foreach (var script in scripts)
            {
                var (logs, finalResponse) = await RunUntilScriptCompletes(service, script.Command, script.Response);

                finalResponse.State.Should().Be(ProcessState.Complete);
                finalResponse.ExitCode.Should().Be(0);
                logs.Count.Should().BeGreaterThan(1);
            }

            started.Stop();
            started.Elapsed.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(20));
        }

        [Test]
        public async Task ShouldStartExecuteAScriptQuickly()
        {
            var builder = new ScriptServiceV2Builder();
            var service = builder.Build();

            var script = "echo \"finished\"";

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(script)
                .WithIsolation(ScriptIsolationLevel.NoIsolation)
                .WithDurationStartScriptCanWaitForScriptToFinish(null)
                .Build();

            var durationUntilScriptStartedRunning = Stopwatch.StartNew();
            var response = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);

            while (response.State != ProcessState.Complete)
            {
                response = await service.GetStatusAsync(new ScriptStatusRequestV2(startScriptCommand.ScriptTicket, response.NextLogSequence), CancellationToken.None);

                if (response.State == ProcessState.Running && durationUntilScriptStartedRunning.IsRunning)
                {
                    durationUntilScriptStartedRunning.Stop();
                }

                if (response.State != ProcessState.Complete)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            }

            await service.CompleteScriptAsync(new CompleteScriptCommandV2(startScriptCommand.ScriptTicket), CancellationToken.None);

            durationUntilScriptStartedRunning.Elapsed.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(10));
        }

        [Test]
        public async Task ShouldExecuteALongRunningScriptSuccessfully()
        {
            var builder = new ScriptServiceV2Builder();
            var service = builder.Build();

            var bashScript = "sleep 10";
            var windowsScript = "Start-Sleep -Seconds 10";

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                .WithIsolation(ScriptIsolationLevel.NoIsolation)
                .WithDurationStartScriptCanWaitForScriptToFinish(null)
                .Build();

            var startScriptResponse = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);
            var (_, finalResponse) = await RunUntilScriptCompletes(service, startScriptCommand, startScriptResponse);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);
        }

        [Test]
        public async Task StartScriptShouldWaitForAShortScriptToFinish()
        {
            var builder = new ScriptServiceV2Builder();
            var service = builder.Build();

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody("echo \"finished\"")
                .WithDurationStartScriptCanWaitForScriptToFinish(TimeSpan.FromSeconds(5))
                .WithIsolation(ScriptIsolationLevel.NoIsolation)
                .Build();

            var startScriptResponse = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);

            startScriptResponse.State.Should().Be(ProcessState.Complete);
            startScriptResponse.ExitCode.Should().Be(0);
            startScriptResponse.Logs.Count.Should().BeGreaterThan(1);

            await service.CompleteScriptAsync(new CompleteScriptCommandV2(startScriptCommand.ScriptTicket), CancellationToken.None);
        }

        [Test]
        public async Task StartScriptShouldNotWaitForALongScriptToFinish()
        {
            var builder = new ScriptServiceV2Builder();
            var service = builder.Build();

            var bashScript = "sleep 10";
            var windowsScript = "Start-Sleep -Seconds 10";

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                .WithDurationStartScriptCanWaitForScriptToFinish(TimeSpan.FromSeconds(5))
                .WithIsolation(ScriptIsolationLevel.NoIsolation)
                .Build();

            var startScriptResponse = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);
            await RunUntilScriptCompletes(service, startScriptCommand, startScriptResponse);

            startScriptResponse.State.Should().Be(ProcessState.Running);
            startScriptResponse.ExitCode.Should().Be(0);
            startScriptResponse.Logs.Count.Should().BeGreaterThan(1);
        }

        [Test]
        public async Task StartScriptShouldNotStartTheScriptForTheSameScriptTicketMoreThanOnce_SequentialRequests()
        {
            var builder = new ScriptServiceV2Builder();
            var service = builder.Build();

            // Arrange
            var scriptTicket = new ScriptTicket(Guid.NewGuid().ToString());
            var script1 = GetStartScriptCommandForScriptThatCreatesAFile(scriptTicket);
            var script2 = GetStartScriptCommandForScriptThatCreatesAFile(scriptTicket);

            // Act
            await service.StartScriptAsync(script1.StartScriptCommand, CancellationToken.None);

            var startScriptResponse = await service.StartScriptAsync(script2.StartScriptCommand, CancellationToken.None);
            var (_, finalResponse) = await RunUntilScriptCompletes(service, script2.StartScriptCommand, startScriptResponse);

            // Assert
            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            File.Exists(script1.FileScriptWillCreate.FullName).Should().BeTrue();
            File.Exists(script2.FileScriptWillCreate.FullName).Should().BeFalse();
        }

        [Test]
        public async Task StartScriptShouldNotStartTheScriptForTheSameScriptTicketMoreThanOnce_ConcurrentRequests()
        {
            var builder = new ScriptServiceV2Builder();
            var service = builder.Build();

            // Arrange
            var scriptTicket = new ScriptTicket(Guid.NewGuid().ToString());

            var scripts = new List<(StartScriptCommandV2 StartScriptCommand, FileInfo FileScriptWillCreate)>();

            for (var i = 0; i < 1000; i++)
            {
                scripts.Add(GetStartScriptCommandForScriptThatCreatesAFile(scriptTicket, scriptDelayInSeconds: 10));
            }

            var tasks = new ConcurrentBag<Task>();

            // Act
            Parallel.ForEach(scripts, x =>
            {
                var task = Task.Run(async () => await service.StartScriptAsync(x.StartScriptCommand, CancellationToken.None));
                tasks.Add(task);
            });

            await Task.WhenAll(tasks);

            var (_, finalResponse) = await RunUntilScriptCompletes(
                service,
                scripts[0].StartScriptCommand,
                new ScriptStatusResponseV2(scripts[0].StartScriptCommand.ScriptTicket, ProcessState.Pending, 0, new List<ProcessOutput>(), 0));

            // Assert
            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            var filesCreated = 0;

            scripts.ForEach(x =>
            {
                if (File.Exists(x.FileScriptWillCreate.FullName))
                {
                    ++filesCreated;
                }
            });
        }

        [Test]
        public async Task CancelScriptShouldCancelAnExecutingScript()
        {
            var builder = new ScriptServiceV2Builder();
            var service = builder.Build();

            var bashScript = "sleep 60";
            var windowsScript = "Start-Sleep -Seconds 60";

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                .WithIsolation(ScriptIsolationLevel.NoIsolation)
                .WithDurationStartScriptCanWaitForScriptToFinish(null)
                .Build();

            var cancellationTimer = new Stopwatch();
            var response = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);
            var logs = new List<ProcessOutput>(response.Logs);

            while (response.State != ProcessState.Complete)
            {
                response = await service.GetStatusAsync(new ScriptStatusRequestV2(startScriptCommand.ScriptTicket, response.NextLogSequence), CancellationToken.None);
                logs.AddRange(response.Logs);

                if (response.State == ProcessState.Running && !cancellationTimer.IsRunning)
                {
                    cancellationTimer.Start();
                    response = await service.CancelScriptAsync(new CancelScriptCommandV2(startScriptCommand.ScriptTicket, response.NextLogSequence), CancellationToken.None);
                    logs.AddRange(response.Logs);
                }

                if (response.State != ProcessState.Complete)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            }

            cancellationTimer.Stop();

            await service.CompleteScriptAsync(new CompleteScriptCommandV2(startScriptCommand.ScriptTicket), CancellationToken.None);

            WriteLogsToConsole(logs);

            response.State.Should().Be(ProcessState.Complete);
            response.ExitCode.Should().NotBe(0, "The exit code varies based on the OS and flakiness with killing the running script");
            cancellationTimer.Elapsed.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(5));
        }

        [Test]
        public async Task AbandonScript_WhileWaitingOnTheIsolationMutex_ExitsCancelled()
        {
            var builder = new ScriptServiceV2Builder();
            var service = builder.Build();

            const string mutexName = "abandon-while-acquiring-mutex";

            // The holder takes the full-isolation mutex and keeps it for the duration of the test.
            var holderTicket = new ScriptTicket(Guid.NewGuid().ToString());
            var holderCommand = new StartScriptCommandV2Builder()
                .WithScriptBodyForCurrentOs("Start-Sleep -Seconds 60", "sleep 60")
                .WithIsolation(ScriptIsolationLevel.FullIsolation)
                .WithMutexName(mutexName)
                .WithScriptTicket(holderTicket)
                .WithDurationStartScriptCanWaitForScriptToFinish(null)
                .Build();

            // The waiter wants the same mutex, so it blocks in Acquire and never starts its process.
            var waiterTicket = new ScriptTicket(Guid.NewGuid().ToString());
            var waiterCommand = new StartScriptCommandV2Builder()
                .WithScriptBodyForCurrentOs("Start-Sleep -Seconds 1", "sleep 1")
                .WithIsolation(ScriptIsolationLevel.FullIsolation)
                .WithMutexName(mutexName)
                .WithScriptTicket(waiterTicket)
                .WithDurationStartScriptCanWaitForScriptToFinish(null)
                .Build();

            try
            {
                var holderResponse = await service.StartScriptAsync(holderCommand, CancellationToken.None);
                holderResponse = await WaitForState(service, holderTicket, holderResponse, ProcessState.Running);

                var waiterResponse = await service.StartScriptAsync(waiterCommand, CancellationToken.None);

                // Give the waiter time to reach (and block in) the mutex Acquire. It cannot complete while the
                // holder owns the mutex, so it must still be waiting.
                await Task.Delay(TimeSpan.FromSeconds(2));
                waiterResponse = await service.GetStatusAsync(new ScriptStatusRequestV2(waiterTicket, waiterResponse.NextLogSequence), CancellationToken.None);
                waiterResponse.State.Should().NotBe(ProcessState.Complete, "the waiter should be blocked acquiring the isolation mutex");

                // A script still waiting on the isolation mutex never started a process, so we report it as
                // cancelled (-43), not abandoned. Abandon is a no-op here (Acquire doesn't observe the abandon
                // token); cancel unblocks Acquire and wins. If we ever want abandon to win in this case,
                // RunningScript.Execute can catch (OperationCanceledException) when (abandonToken.IsCancellationRequested)
                // around the mutex Acquire and return AbandonedExitCode (-48) instead. Deliberately not doing that today.
                await service.AbandonScriptAsync(new AbandonScriptCommandV2(waiterTicket, waiterResponse.NextLogSequence), CancellationToken.None);
                var afterCancel = await service.CancelScriptAsync(new CancelScriptCommandV2(waiterTicket, waiterResponse.NextLogSequence), CancellationToken.None);

                var (_, waiterFinal) = await RunUntilScriptCompletes(service, waiterCommand, afterCancel);
                waiterFinal.ExitCode.Should().Be(ScriptExitCodes.CanceledExitCode);
            }
            finally
            {
                // Release the holder so we don't sit on the mutex (and its 60s sleep gets killed).
                await service.CancelScriptAsync(new CancelScriptCommandV2(holderTicket, 0), CancellationToken.None);
                await service.CompleteScriptAsync(new CompleteScriptCommandV2(holderTicket), CancellationToken.None);
            }
        }

        [Test]
        public async Task CompleteScriptShouldCleanupTheWorkspace()
        {
            var builder = new ScriptServiceV2Builder();
            var service = builder.Build();
            var workspaceFactory = (ScriptWorkspaceFactory)builder.WorkspaceFactory;

            var script = "echo \"finished\"";

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(script)
                .WithIsolation(ScriptIsolationLevel.NoIsolation)
                .WithDurationStartScriptCanWaitForScriptToFinish(null)
                .Build();

            var workspaceDirectory = workspaceFactory.GetWorkingDirectoryPath(startScriptCommand.ScriptTicket);
            Directory.Exists(workspaceDirectory).Should().BeFalse();

            var startScriptResponse = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);
            Directory.Exists(workspaceDirectory).Should().BeTrue();

            await RunUntilScriptCompletes(service, startScriptCommand, startScriptResponse);
            Directory.Exists(workspaceDirectory).Should().BeFalse();
        }

        [Test]
        public async Task GetStatusShouldReturnAnExitCodeOf45ForAnUnknownScriptTicket()
        {
            var service = new ScriptServiceV2Builder().Build();
            var response = await service.GetStatusAsync(new ScriptStatusRequestV2(new ScriptTicket("nope"), 0), CancellationToken.None);

            response.ExitCode.Should().Be(-45);
        }

        [Test]
        public async Task CancelScriptShouldReturnAnExitCodeOf45ForAnUnknownScriptTicket()
        {
            var service = new ScriptServiceV2Builder().Build();
            var response = await service.CancelScriptAsync(new CancelScriptCommandV2(new ScriptTicket("nope"), 0), CancellationToken.None);

            response.ExitCode.Should().Be(-45);
        }

        [Test]
        public async Task CompleteScriptShouldNotErrorForAnUnknownScriptTicket()
        {
            var service = new ScriptServiceV2Builder().Build();
            await service.CompleteScriptAsync(new CompleteScriptCommandV2(new ScriptTicket("nope")), CancellationToken.None);
        }

        [Test]
        public async Task GetStatusShouldReturnAnExitCodeOf46ForAScriptWithAnUnknownResult()
        {
            var builder = new ScriptServiceV2Builder();
            var service = builder.Build();

            var request = new ScriptStatusRequestV2(new ScriptTicket($"did-not-finish-{Guid.NewGuid()}"), 0);
            var ticket = request.Ticket;
            SetupScriptState(builder.WorkspaceFactory, builder.StateStoreFactory, ticket);

            var response = await service.GetStatusAsync(request, CancellationToken.None);

            response.ExitCode.Should().Be(-46);

            await CleanupWorkspace(builder.WorkspaceFactory, ticket, CancellationToken.None);
        }

        [Test]
        public async Task CancelScriptShouldReturnAnExitCodeOf46ForAScriptWithAnUnknownResult()
        {
            var builder = new ScriptServiceV2Builder();
            var service = builder.Build();

            var request = new CancelScriptCommandV2(new ScriptTicket($"did-not-finish-{Guid.NewGuid()}"), 0);
            var ticket = request.Ticket;
            SetupScriptState(builder.WorkspaceFactory, builder.StateStoreFactory, ticket);

            var response = await service.CancelScriptAsync(request, CancellationToken.None);

            response.ExitCode.Should().Be(-46);

            await CleanupWorkspace(builder.WorkspaceFactory, ticket, CancellationToken.None);
        }

        [Test]
        public async Task CompleteScriptShouldNotErrorForAScriptWithAnUnknownResult()
        {
            var builder = new ScriptServiceV2Builder();
            var service = builder.Build();

            var request = new CompleteScriptCommandV2(new ScriptTicket($"did-not-finish-{Guid.NewGuid()}"));
            var ticket = request.Ticket;
            SetupScriptState(builder.WorkspaceFactory, builder.StateStoreFactory, ticket);

            await service.CompleteScriptAsync(request, CancellationToken.None);
        }

        [Test]
        public async Task ShouldStoreTheStateOfTheScriptInTheScriptStateStore()
        {
            var builder = new ScriptServiceV2Builder();
            var service = builder.Build();

            var testStarted = DateTimeOffset.UtcNow;

            var bashScript = "sleep 10";
            var windowsScript = "Start-Sleep -Seconds 10";

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                .WithIsolation(ScriptIsolationLevel.NoIsolation)
                .WithDurationStartScriptCanWaitForScriptToFinish(null)
                .Build();

            var scriptStateStore = SetupScriptStateStore(builder.WorkspaceFactory, builder.StateStoreFactory, startScriptCommand.ScriptTicket);

            var startScriptResponse = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);

            var runningScriptState = scriptStateStore.Load();

            var (logs, finalResponse) = await RunUntilScriptFinishes(service, startScriptCommand, startScriptResponse);

            var testFinished = DateTimeOffset.UtcNow;
            var finishedScriptState = scriptStateStore.Load();

            await service.CompleteScriptAsync(new CompleteScriptCommandV2(startScriptCommand.ScriptTicket), CancellationToken.None);
            WriteLogsToConsole(logs);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            runningScriptState.Completed.Should().BeNull();
            runningScriptState.ExitCode.Should().BeNull();
            runningScriptState.RanToCompletion.Should().BeNull();
            runningScriptState.Created.Should().BeOnOrAfter(testStarted).And.BeOnOrBefore(testFinished);

            finishedScriptState.Completed.Should().BeOnOrAfter(testStarted.AddSeconds(5)).And.BeOnOrBefore(testFinished);
            finishedScriptState.ExitCode.Should().Be(0);
            finishedScriptState.RanToCompletion.Should().BeTrue();
            finishedScriptState.Created.Should().Be(runningScriptState.Created);
            finishedScriptState.Started.Should().BeOnOrAfter(testStarted).And.BeOnOrBefore(testFinished);
        }

        [Test]
        public async Task ScriptTicketCasingShouldNotAffectCommands()
        {
            var service = new ScriptServiceV2Builder().Build();

            // Arrange
            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody("echo \"finished\"")
                .WithIsolation(ScriptIsolationLevel.NoIsolation)
                .WithDurationStartScriptCanWaitForScriptToFinish(null)
                .Build();

            var response = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);

            var upperCaseScriptTicket = new ScriptTicket(startScriptCommand.ScriptTicket.TaskId.ToUpper());
            var lowerCaseScriptTicket = new ScriptTicket(startScriptCommand.ScriptTicket.TaskId.ToLower());

            // Act
            var upperCaseResponse = await service.GetStatusAsync(new ScriptStatusRequestV2(upperCaseScriptTicket, response.NextLogSequence), CancellationToken.None);
            var lowerCaseResponse = await service.GetStatusAsync(new ScriptStatusRequestV2(lowerCaseScriptTicket, response.NextLogSequence), CancellationToken.None);
            await service.CompleteScriptAsync(new CompleteScriptCommandV2(startScriptCommand.ScriptTicket), CancellationToken.None);

            // Assert
            upperCaseResponse.ExitCode.Should().Be(0);
            lowerCaseResponse.ExitCode.Should().Be(0);
        }

        [Test]
        public async Task AbandonScript_OnUnknownTicket_ReturnsCompleteWithUnknownScriptExitCode()
        {
            var service = new ScriptServiceV2Builder().Build();

            var ticket = new ScriptTicket("unknown-ticket-" + Guid.NewGuid().ToString("N"));
            var response = await service.AbandonScriptAsync(new AbandonScriptCommandV2(ticket, 0), CancellationToken.None);

            response.State.Should().Be(ProcessState.Complete);
            response.ExitCode.Should().Be(ScriptExitCodes.UnknownScriptExitCode);
        }

        [Test]
        public async Task AbandonScript_OnRunningScript_FiresAbandonToken_ReturnsAbandonedExitCode()
        {
            var service = new ScriptServiceV2Builder().Build();

            var startCommand = new StartScriptCommandV2Builder()
                .WithScriptBodyForCurrentOs("Start-Sleep -Seconds 60", "sleep 60")
                .WithIsolation(ScriptIsolationLevel.NoIsolation)
                .WithDurationStartScriptCanWaitForScriptToFinish(null)
                .Build();

            await service.StartScriptAsync(startCommand, CancellationToken.None);

            // Wait for the script to reach Running state
            ScriptStatusResponseV2 status;
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do
            {
                status = await service.GetStatusAsync(new ScriptStatusRequestV2(startCommand.ScriptTicket, 0), CancellationToken.None);
                if (status.State == ProcessState.Running) break;
                await Task.Delay(50);
            } while (DateTime.UtcNow < deadline);
            status.State.Should().Be(ProcessState.Running, "script should have reached Running state within 30 seconds");

            // Fire abandon
            await service.AbandonScriptAsync(new AbandonScriptCommandV2(startCommand.ScriptTicket, 0), CancellationToken.None);

            // Poll until the script completes (the abandon token causes the process runner to return AbandonedExitCode)
            ScriptStatusResponseV2 finalResponse;
            var completionDeadline = DateTime.UtcNow.AddSeconds(30);
            do
            {
                finalResponse = await service.GetStatusAsync(new ScriptStatusRequestV2(startCommand.ScriptTicket, 0), CancellationToken.None);
                if (finalResponse.State == ProcessState.Complete) break;
                await Task.Delay(100);
            } while (DateTime.UtcNow < completionDeadline);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(ScriptExitCodes.AbandonedExitCode);
        }

        [Test]
        public async Task AbandonScript_OnAlreadyCompletedScript_ReturnsRealExitCode()
        {
            var service = new ScriptServiceV2Builder().Build();

            var startCommand = new StartScriptCommandV2Builder()
                .WithScriptBody("echo \"finished\"")
                .WithIsolation(ScriptIsolationLevel.NoIsolation)
                .WithDurationStartScriptCanWaitForScriptToFinish(null)
                .Build();

            await service.StartScriptAsync(startCommand, CancellationToken.None);

            // Wait for the script to complete
            ScriptStatusResponseV2 status;
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do
            {
                status = await service.GetStatusAsync(new ScriptStatusRequestV2(startCommand.ScriptTicket, 0), CancellationToken.None);
                if (status.State == ProcessState.Complete) break;
                await Task.Delay(50);
            } while (DateTime.UtcNow < deadline);
            status.State.Should().Be(ProcessState.Complete);

            var abandonResponse = await service.AbandonScriptAsync(new AbandonScriptCommandV2(startCommand.ScriptTicket, 0), CancellationToken.None);
            abandonResponse.ExitCode.Should().Be(0, "real exit code should be returned, not AbandonedExitCode");
        }

        [Test]
        public async Task CompleteScript_AfterAbandon_WhenWorkspaceDeleteFails_LogsWarnAndReturnsNormally()
        {
            var deleteException = new IOException("file in use");
            var builder = new ScriptServiceV2Builder();
            var (throwingFactory, mockLog) = BuildFactoryWithThrowingDelete(builder.WorkspaceFactory, deleteException);
            var serviceUnderTest = builder
                .WithWorkspaceFactory(throwingFactory)
                .WithLog(mockLog)
                .Build();

            var startCommand = new StartScriptCommandV2Builder()
                .WithScriptBodyForCurrentOs("Start-Sleep -Seconds 60", "sleep 60")
                .WithIsolation(ScriptIsolationLevel.NoIsolation)
                .WithDurationStartScriptCanWaitForScriptToFinish(null)
                .Build();

            await serviceUnderTest.StartScriptAsync(startCommand, CancellationToken.None);

            // Wait for Running
            ScriptStatusResponseV2 status;
            var runningDeadline = DateTime.UtcNow.AddSeconds(30);
            do
            {
                status = await serviceUnderTest.GetStatusAsync(new ScriptStatusRequestV2(startCommand.ScriptTicket, 0), CancellationToken.None);
                if (status.State == ProcessState.Running) break;
                await Task.Delay(50);
            } while (DateTime.UtcNow < runningDeadline);
            status.State.Should().Be(ProcessState.Running, "script should have reached Running state within 30 seconds");

            await serviceUnderTest.AbandonScriptAsync(new AbandonScriptCommandV2(startCommand.ScriptTicket, 0), CancellationToken.None);

            // Poll until Complete
            var completeDeadline = DateTime.UtcNow.AddSeconds(30);
            do
            {
                status = await serviceUnderTest.GetStatusAsync(new ScriptStatusRequestV2(startCommand.ScriptTicket, 0), CancellationToken.None);
                if (status.State == ProcessState.Complete) break;
                await Task.Delay(50);
            } while (DateTime.UtcNow < completeDeadline);
            status.State.Should().Be(ProcessState.Complete);
            status.ExitCode.Should().Be(ScriptExitCodes.AbandonedExitCode);

            Func<Task> complete = async () => await serviceUnderTest.CompleteScriptAsync(new CompleteScriptCommandV2(startCommand.ScriptTicket), CancellationToken.None);

            await complete.Should().NotThrowAsync();
            mockLog.Received().Warn(deleteException, Arg.Is<string>(m => m.Contains("Could not delete") && m.Contains(startCommand.ScriptTicket.TaskId)));
        }

        [Test]
        public async Task CompleteScript_AfterNormalCompletion_WhenWorkspaceDeleteFails_PropagatesException()
        {
            var deleteException = new IOException("file in use");
            var builder = new ScriptServiceV2Builder();
            var (throwingFactory, mockLog) = BuildFactoryWithThrowingDelete(builder.WorkspaceFactory, deleteException);
            var serviceUnderTest = builder
                .WithWorkspaceFactory(throwingFactory)
                .WithLog(mockLog)
                .Build();

            var startCommand = new StartScriptCommandV2Builder()
                .WithScriptBody("echo \"finished\"")
                .WithIsolation(ScriptIsolationLevel.NoIsolation)
                .WithDurationStartScriptCanWaitForScriptToFinish(null)
                .Build();

            await serviceUnderTest.StartScriptAsync(startCommand, CancellationToken.None);

            // Poll until natural completion
            ScriptStatusResponseV2 status;
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do
            {
                status = await serviceUnderTest.GetStatusAsync(new ScriptStatusRequestV2(startCommand.ScriptTicket, 0), CancellationToken.None);
                if (status.State == ProcessState.Complete) break;
                await Task.Delay(50);
            } while (DateTime.UtcNow < deadline);
            status.State.Should().Be(ProcessState.Complete);
            status.ExitCode.Should().Be(0, "the script exited cleanly, not via abandon");

            Func<Task> complete = async () => await serviceUnderTest.CompleteScriptAsync(new CompleteScriptCommandV2(startCommand.ScriptTicket), CancellationToken.None);

            await complete.Should().ThrowAsync<IOException>();
        }

        /// <summary>
        /// Builds an IScriptWorkspaceFactory decorator over the supplied workspaceFactory whose returned
        /// workspaces forward every member except Delete(CancellationToken), which throws the supplied
        /// exception. Also returns a mock ISystemLog for assertion.
        /// </summary>
        static (IScriptWorkspaceFactory factory, ISystemLog log) BuildFactoryWithThrowingDelete(IScriptWorkspaceFactory workspaceFactory, Exception deleteException)
        {
            var throwingFactory = new DeleteThrowingScriptWorkspaceFactory(workspaceFactory, deleteException);
            var fakeLog = Substitute.For<ISystemLog>();
            return (throwingFactory, fakeLog);
        }

        /// <summary>
        /// Builder for ScriptServiceV2 SUT construction. Defaults match what the previous [SetUp]
        /// produced; tests opt into mock overrides via the chainable With* methods. The
        /// WorkspaceFactory and StateStoreFactory properties materialize on first access so tests
        /// can grab them before Build() (e.g. to wrap the default factory in a decorator).
        /// </summary>
        class ScriptServiceV2Builder
        {
            IScriptWorkspaceFactory? workspaceFactory;
            ScriptStateStoreFactory? stateStoreFactory;
            ISystemLog? log;
            IShell? shell;
            ScriptIsolationMutex? mutex;
            OctopusPhysicalFileSystem? cachedFileSystem;

            public IScriptWorkspaceFactory WorkspaceFactory => workspaceFactory ??= BuildDefaultWorkspaceFactory();
            public ScriptStateStoreFactory StateStoreFactory => stateStoreFactory ??= new ScriptStateStoreFactory(FileSystem);

            OctopusPhysicalFileSystem FileSystem => cachedFileSystem ??= new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>());

            ScriptWorkspaceFactory BuildDefaultWorkspaceFactory()
            {
                var homeConfiguration = Substitute.For<IHomeConfiguration>();
                homeConfiguration.HomeDirectory.Returns(Environment.CurrentDirectory);
                return new ScriptWorkspaceFactory(FileSystem, homeConfiguration, new SensitiveValueMasker());
            }

            public ScriptServiceV2Builder WithWorkspaceFactory(IScriptWorkspaceFactory factory)
            {
                workspaceFactory = factory;
                return this;
            }

            public ScriptServiceV2Builder WithStateStoreFactory(ScriptStateStoreFactory factory)
            {
                stateStoreFactory = factory;
                return this;
            }

            public ScriptServiceV2Builder WithLog(ISystemLog log)
            {
                this.log = log;
                return this;
            }

            public ScriptServiceV2Builder WithShell(IShell shell)
            {
                this.shell = shell;
                return this;
            }

            public ScriptServiceV2Builder WithMutex(ScriptIsolationMutex mutex)
            {
                this.mutex = mutex;
                return this;
            }

            public ScriptServiceV2 Build()
            {
                return new ScriptServiceV2(
                    shell ?? (PlatformDetection.IsRunningOnWindows ? (IShell)new PowerShell() : new Bash()),
                    WorkspaceFactory,
                    StateStoreFactory,
                    mutex ?? new ScriptIsolationMutex(),
                    log ?? Substitute.For<ISystemLog>());
            }
        }

        /// <summary>
        /// IScriptWorkspaceFactory decorator that wraps every workspace it returns in a
        /// DeleteThrowingScriptWorkspace so that Delete throws the configured exception while all
        /// other members forward to the real workspace.
        /// </summary>
        class DeleteThrowingScriptWorkspaceFactory : IScriptWorkspaceFactory
        {
            readonly IScriptWorkspaceFactory inner;
            readonly Exception deleteException;

            public DeleteThrowingScriptWorkspaceFactory(IScriptWorkspaceFactory inner, Exception deleteException)
            {
                this.inner = inner;
                this.deleteException = deleteException;
            }

            public IScriptWorkspace GetWorkspace(ScriptTicket ticket, WorkspaceReadinessCheck readinessCheck)
                => new DeleteThrowingScriptWorkspace(inner.GetWorkspace(ticket, readinessCheck), deleteException);

            public async Task<IScriptWorkspace> PrepareWorkspace(
                ScriptTicket ticket,
                string scriptBody,
                Dictionary<ScriptType, string> scripts,
                ScriptIsolationLevel isolationLevel,
                TimeSpan scriptMutexAcquireTimeout,
                string? scriptMutexName,
                string[]? scriptArguments,
                List<ScriptFile> files,
                CancellationToken cancellationToken)
            {
                var workspace = await inner.PrepareWorkspace(ticket, scriptBody, scripts, isolationLevel, scriptMutexAcquireTimeout, scriptMutexName, scriptArguments, files, cancellationToken);
                return new DeleteThrowingScriptWorkspace(workspace, deleteException);
            }

            public List<IScriptWorkspace> GetUncompletedWorkspaces()
                => inner.GetUncompletedWorkspaces().Select(w => (IScriptWorkspace)new DeleteThrowingScriptWorkspace(w, deleteException)).ToList();
        }

        /// <summary>
        /// IScriptWorkspace decorator that forwards every member to an inner real workspace, except
        /// Delete(CancellationToken), which throws the configured exception. Used to exercise the
        /// CompleteScript abandon-aware tolerance of Delete failures without disturbing anything else
        /// StartScript / RunningScript may touch on the workspace.
        /// </summary>
        class DeleteThrowingScriptWorkspace : IScriptWorkspace
        {
            readonly IScriptWorkspace inner;
            readonly Exception deleteException;

            public DeleteThrowingScriptWorkspace(IScriptWorkspace inner, Exception deleteException)
            {
                this.inner = inner;
                this.deleteException = deleteException;
            }

            public ScriptTicket ScriptTicket => inner.ScriptTicket;
            public string WorkingDirectory => inner.WorkingDirectory;
            public string BootstrapScriptFilePath => inner.BootstrapScriptFilePath;
            public string LogFilePath => inner.LogFilePath;

            public string[]? ScriptArguments
            {
                get => inner.ScriptArguments;
                set => inner.ScriptArguments = value;
            }

            public ScriptIsolationLevel IsolationLevel
            {
                get => inner.IsolationLevel;
                set => inner.IsolationLevel = value;
            }

            public TimeSpan ScriptMutexAcquireTimeout
            {
                get => inner.ScriptMutexAcquireTimeout;
                set => inner.ScriptMutexAcquireTimeout = value;
            }

            public string? ScriptMutexName
            {
                get => inner.ScriptMutexName;
                set => inner.ScriptMutexName = value;
            }

            public bool ShouldMonitorPowerShellStartup() => inner.ShouldMonitorPowerShellStartup();
            public void BootstrapScript(string scriptBody) => inner.BootstrapScript(scriptBody);
            public string ResolvePath(string fileName) => inner.ResolvePath(fileName);
            public IScriptLog CreateLog() => inner.CreateLog();
            public void WriteFile(string filename, string contents) => inner.WriteFile(filename, contents);
            public void CopyFile(string sourceFilePath, string destFileName, bool overwrite) => inner.CopyFile(sourceFilePath, destFileName, overwrite);
            public void CheckReadiness() => inner.CheckReadiness();
            public string? TryReadFile(string filename) => inner.TryReadFile(filename);

            public Task Delete(CancellationToken cancellationToken) => throw deleteException;
        }

        // TODO - Test the stateStore is updated.

        static void SetupScriptState(IScriptWorkspaceFactory workspaceFactory, ScriptStateStoreFactory stateStoreFactory, ScriptTicket ticket)
        {
            var stateWorkspace = SetupScriptStateStore(workspaceFactory, stateStoreFactory, ticket);
            stateWorkspace.Create();
        }

        static ScriptStateStore SetupScriptStateStore(IScriptWorkspaceFactory workspaceFactory, ScriptStateStoreFactory stateStoreFactory, ScriptTicket ticket)
        {
            var workspace = workspaceFactory.GetWorkspace(ticket, WorkspaceReadinessCheck.Perform);
            var stateWorkspace = stateStoreFactory.Create(workspace);
            return stateWorkspace;
        }

        static async Task CleanupWorkspace(IScriptWorkspaceFactory workspaceFactory, ScriptTicket ticket, CancellationToken cancellationToken)
        {
            var workspace = workspaceFactory.GetWorkspace(ticket, WorkspaceReadinessCheck.Skip);
            await workspace.Delete(cancellationToken);
        }

        (StartScriptCommandV2 StartScriptCommand, FileInfo FileScriptWillCreate) GetStartScriptCommandForScriptThatCreatesAFile(ScriptTicket scriptTicket, int? scriptDelayInSeconds = null)
        {
            var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            var bashScript = @$"echo ""Started 1"" > ""{filePath}""";
            var windowsScript = @$"echo ""Started 1"" > ""{filePath}""";

            if (scriptDelayInSeconds != null)
            {
                bashScript += $"{Environment.NewLine} sleep {scriptDelayInSeconds}";
                windowsScript += $"{Environment.NewLine} Start-Sleep -Seconds {scriptDelayInSeconds}";
            }

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                .WithScriptTicket(scriptTicket)
                .WithIsolation(ScriptIsolationLevel.NoIsolation)
                .WithDurationStartScriptCanWaitForScriptToFinish(null)
                .Build();

            return (startScriptCommand, new FileInfo(filePath));
        }

        static async Task<(List<ProcessOutput>, ScriptStatusResponseV2)> RunUntilScriptCompletes(ScriptServiceV2 service, StartScriptCommandV2 startScriptCommand, ScriptStatusResponseV2 response)
        {
            var (logs, lastResponse) = await RunUntilScriptFinishes(service, startScriptCommand, response);

            await service.CompleteScriptAsync(new CompleteScriptCommandV2(startScriptCommand.ScriptTicket), CancellationToken.None);

            WriteLogsToConsole(logs);

            return (logs, lastResponse);
        }

        static async Task<(List<ProcessOutput> logs, ScriptStatusResponseV2 response)> RunUntilScriptFinishes(ScriptServiceV2 service, StartScriptCommandV2 startScriptCommand, ScriptStatusResponseV2 response)
        {
            var logs = new List<ProcessOutput>(response.Logs);

            while (response.State != ProcessState.Complete)
            {
                response = await service.GetStatusAsync(new ScriptStatusRequestV2(startScriptCommand.ScriptTicket, response.NextLogSequence), CancellationToken.None);

                logs.AddRange(response.Logs);

                if (response.State != ProcessState.Complete)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            }

            return (logs, response);
        }

        static async Task<ScriptStatusResponseV2> WaitForState(ScriptServiceV2 service, ScriptTicket ticket, ScriptStatusResponseV2 response, ProcessState desiredState)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            while (response.State != desiredState && DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                response = await service.GetStatusAsync(new ScriptStatusRequestV2(ticket, response.NextLogSequence), CancellationToken.None);
            }

            response.State.Should().Be(desiredState, $"the script should reach {desiredState} within the timeout");
            return response;
        }

        static void WriteLogsToConsole(List<ProcessOutput> logs)
        {
            foreach (var log in logs)
            {
                TestContext.Out.WriteLine("{0:yyyy-MM-dd HH:mm:ss K}: {1}", log.Occurred.ToLocalTime(), log.Text);
            }
        }

        class StartScriptCommandAndResponse
        {
            public StartScriptCommandAndResponse(StartScriptCommandV2 command)
            {
                Command = command;
            }

            public StartScriptCommandV2 Command { get; }
            public ScriptStatusResponseV2? Response { get; set; }
        }
    }
}
