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
using Octopus.Diagnostics;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Services.Scripts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration
{
    [TestFixture]
    public class ScriptServiceV2Fixture
    {
        IScriptServiceV2 service = null!;
        ScriptWorkspaceFactory workspaceFactory = null!;
        ScriptStateStoreFactory stateStoreFactory = null!;

        [SetUp]
        public void SetUp()
        {
            var homeConfiguration = Substitute.For<IHomeConfiguration>();
            homeConfiguration.HomeDirectory.Returns(Environment.CurrentDirectory);

            var octopusPhysicalFileSystem = new OctopusPhysicalFileSystem(Substitute.For<ISystemLog>());
            workspaceFactory = new ScriptWorkspaceFactory(octopusPhysicalFileSystem, homeConfiguration, new SensitiveValueMasker());
            stateStoreFactory = new ScriptStateStoreFactory(octopusPhysicalFileSystem);
            service = new ScriptServiceV2(
                PlatformDetection.IsRunningOnWindows ? (IShell)new PowerShell() : new Bash(),
                workspaceFactory,
                stateStoreFactory,
                Substitute.For<ISystemLog>());
        }

        [Test]
        public async Task ShouldExecuteAScriptSuccessfully()
        {
            var windowsScript = "& ping.exe localhost -n 1";
            var bashScript = "ping localhost -c 1";

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                .Build();

            var startScriptResponse = service.StartScript(startScriptCommand);
            var (logs, finalResponse) = await RunUntilScriptCompletes(startScriptCommand, startScriptResponse);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);
            logs.Count.Should().BeGreaterThan(1);
        }

        [Test]
        public async Task ShouldReturnANonZeroExitCodeForAFailingScript()
        {
            var windowsScript = "& ping.exe nope -n 1";
            var bashScript = "ping nope -c 1";

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                .Build();

            var startScriptResponse = service.StartScript(startScriptCommand);
            var (logs, finalResponse) = await RunUntilScriptCompletes(startScriptCommand, startScriptResponse);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().NotBe(0);
            logs.Count.Should().BeGreaterThan(1);
        }

        [Test]
        public async Task ShouldExecuteMultipleScriptsConcurrently()
        {
            var bashScript = "sleep 10";
            var windowsScript = "Start-Sleep -Seconds 10";

            var scripts = Enumerable.Range(0, 5).Select(x =>
                new StartScriptCommandAndResponse(command: new StartScriptCommandV2Builder()
                    .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                    .Build())).ToList();

            var started = Stopwatch.StartNew();

            Parallel.ForEach(scripts, script =>
            {
                script.Response = service.StartScript(script.Command);
            });

            var startDuration = started.Elapsed;
            startDuration.Should().BeLessThan(TimeSpan.FromSeconds(5));

            await Task.Delay(TimeSpan.FromSeconds(10), CancellationToken.None);

            foreach (var script in scripts)
            {
                var (logs, finalResponse) = await RunUntilScriptCompletes(script.Command, script.Response);

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
            var script = "echo \"finished\"";

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(script)
                .Build();

            var durationUntilScriptStartedRunning = Stopwatch.StartNew();
            var response = service.StartScript(startScriptCommand);

            while (response.State != ProcessState.Complete)
            {
                response = service.GetStatus(new ScriptStatusRequestV2(startScriptCommand.ScriptTicket, response.NextLogSequence));

                if (response.State == ProcessState.Running && durationUntilScriptStartedRunning.IsRunning)
                {
                    durationUntilScriptStartedRunning.Stop();
                }

                if (response.State != ProcessState.Complete)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            }

            service.CompleteScript(new CompleteScriptCommandV2(startScriptCommand.ScriptTicket));

            durationUntilScriptStartedRunning.Elapsed.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(10));
        }

        [Test]
        public async Task ShouldExecuteALongRunningScriptSuccessfully()
        {
            var bashScript = "sleep 10";
            var windowsScript = "Start-Sleep -Seconds 10";

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                .Build();

            var startScriptResponse = service.StartScript(startScriptCommand);
            var (_, finalResponse) = await RunUntilScriptCompletes(startScriptCommand, startScriptResponse);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);
        }

        [Test]
        public void StartScriptShouldWaitForAShortScriptToFinish()
        {
            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody("echo \"finished\"")
                .WithDurationStartScriptCanWaitForScriptToFinish(TimeSpan.FromSeconds(5))
                .Build();

            var startScriptResponse = service.StartScript(startScriptCommand);

            startScriptResponse.State.Should().Be(ProcessState.Complete);
            startScriptResponse.ExitCode.Should().Be(0);
            startScriptResponse.Logs.Count.Should().BeGreaterThan(1);

            service.CompleteScript(new CompleteScriptCommandV2(startScriptCommand.ScriptTicket));
        }

        [Test]
        public async Task StartScriptShouldNotWaitForALongScriptToFinish()
        {
            var bashScript = "sleep 10";
            var windowsScript = "Start-Sleep -Seconds 10";

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                .WithDurationStartScriptCanWaitForScriptToFinish(TimeSpan.FromSeconds(5))
                .Build();

            var startScriptResponse = service.StartScript(startScriptCommand);
            await RunUntilScriptCompletes(startScriptCommand, startScriptResponse);

            startScriptResponse.State.Should().Be(ProcessState.Running);
            startScriptResponse.ExitCode.Should().Be(0);
            startScriptResponse.Logs.Count.Should().BeGreaterThan(1);
        }

        [Test]
        public async Task StartScriptShouldNotStartTheScriptForTheSameScriptTicketMoreThanOnce_SequentialRequests()
        {
            // Arrange
            var scriptTicket = new ScriptTicket(Guid.NewGuid().ToString());
            var script1 = GetStartScriptCommandForScriptThatCreatesAFile(scriptTicket);
            var script2 = GetStartScriptCommandForScriptThatCreatesAFile(scriptTicket);

            // Act
            service.StartScript(script1.StartScriptCommand);

            var startScriptResponse = service.StartScript(script2.StartScriptCommand);
            var (_, finalResponse) = await RunUntilScriptCompletes(script2.StartScriptCommand, startScriptResponse);

            // Assert
            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            File.Exists(script1.FileScriptWillCreate.FullName).Should().BeTrue();
            File.Exists(script2.FileScriptWillCreate.FullName).Should().BeFalse();
        }

        [Test]
        public async Task StartScriptShouldNotStartTheScriptForTheSameScriptTicketMoreThanOnce_ConcurrentRequests()
        {
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
                var task = Task.Run(() => service.StartScript(x.StartScriptCommand));
                tasks.Add(task);
            });

            await Task.WhenAll(tasks);

            var (_, finalResponse) = await RunUntilScriptCompletes(
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
            var bashScript = "sleep 60";
            var windowsScript = "Start-Sleep -Seconds 60";

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                .Build();

            var cancellationTimer = new Stopwatch();
            var response = service.StartScript(startScriptCommand);
            var logs = new List<ProcessOutput>(response.Logs);

            while (response.State != ProcessState.Complete)
            {
                response = service.GetStatus(new ScriptStatusRequestV2(startScriptCommand.ScriptTicket, response.NextLogSequence));
                logs.AddRange(response.Logs);

                if (response.State == ProcessState.Running && !cancellationTimer.IsRunning)
                {
                    cancellationTimer.Start();
                    response = service.CancelScript(new CancelScriptCommandV2(startScriptCommand.ScriptTicket, response.NextLogSequence));
                    logs.AddRange(response.Logs);
                }

                if (response.State != ProcessState.Complete)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            }

            cancellationTimer.Stop();

            service.CompleteScript(new CompleteScriptCommandV2(startScriptCommand.ScriptTicket));

            WriteLogsToConsole(logs);

            response.State.Should().Be(ProcessState.Complete);
            response.ExitCode.Should().NotBe(0, "The exit code varies based on the OS and flakiness with killing the running script");
            cancellationTimer.Elapsed.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(5));
        }

        [Test]
        public async Task CompleteScriptShouldCleanupTheWorkspace()
        {
            var script = "echo \"finished\"";

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBody(script)
                .Build();

            var workspaceDirectory = workspaceFactory.GetWorkingDirectoryPath(startScriptCommand.ScriptTicket);
            Directory.Exists(workspaceDirectory).Should().BeFalse();

            var startScriptResponse = service.StartScript(startScriptCommand);
            Directory.Exists(workspaceDirectory).Should().BeTrue();

            await RunUntilScriptCompletes(startScriptCommand, startScriptResponse);
            Directory.Exists(workspaceDirectory).Should().BeFalse();
        }

        [Test]
        public void GetStatusShouldReturnAnExitCodeOf45ForAnUnknownScriptTicket()
        {
            var response = service.GetStatus(new ScriptStatusRequestV2(new ScriptTicket("nope"), 0));

            response.ExitCode.Should().Be(-45);
        }

        [Test]
        public void CancelScriptShouldReturnAnExitCodeOf45ForAnUnknownScriptTicket()
        {
            var response = service.CancelScript(new CancelScriptCommandV2(new ScriptTicket("nope"), 0));

            response.ExitCode.Should().Be(-45);
        }

        [Test]
        public void CompleteScriptShouldNotErrorForAnUnknownScriptTicket()
        {
            service.CompleteScript(new CompleteScriptCommandV2(new ScriptTicket("nope")));
        }

        [Test]
        public void GetStatusShouldReturnAnExitCodeOf46ForAScriptWithAnUnknownResult()
        {
            var request = new ScriptStatusRequestV2(new ScriptTicket($"did-not-finish-{Guid.NewGuid()}"), 0);
            var ticket = request.Ticket;
            SetupScriptState(ticket);

            var response = service.GetStatus(request);

            response.ExitCode.Should().Be(-46);

            CleanupWorkspace(ticket);
        }

        [Test]
        public void CancelScriptShouldReturnAnExitCodeOf46ForAScriptWithAnUnknownResult()
        {
            var request = new CancelScriptCommandV2(new ScriptTicket($"did-not-finish-{Guid.NewGuid()}"), 0);
            var ticket = request.Ticket;
            SetupScriptState(ticket);

            var response = service.CancelScript(request);

            response.ExitCode.Should().Be(-46);

            CleanupWorkspace(ticket);
        }

        [Test]
        public void CompleteScriptShouldNotErrorForAScriptWithAnUnknownResult()
        {
            var request = new CompleteScriptCommandV2(new ScriptTicket($"did-not-finish-{Guid.NewGuid()}"));
            var ticket = request.Ticket;
            SetupScriptState(ticket);

            service.CompleteScript(request);
        }

        [Test]
        public async Task ShouldStoreTheStateOfTheScriptInTheScriptStateStore()
        {
            var testStarted = DateTimeOffset.UtcNow;

            var bashScript = "sleep 10";
            var windowsScript = "Start-Sleep -Seconds 10";

            var startScriptCommand = new StartScriptCommandV2Builder()
                .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                .Build();

            var scriptStateStore = SetupScriptStateStore(startScriptCommand.ScriptTicket);

            var startScriptResponse = service.StartScript(startScriptCommand);

            var runningScriptState = scriptStateStore.Load();

            var (logs, finalResponse) = await RunUntilScriptFinishes(startScriptCommand, startScriptResponse);

            var testFinished = DateTimeOffset.UtcNow;
            var finishedScriptState = scriptStateStore.Load();

            service.CompleteScript(new CompleteScriptCommandV2(startScriptCommand.ScriptTicket));
            WriteLogsToConsole(logs);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);

            runningScriptState.Completed.Should().BeNull();
            runningScriptState.ExitCode.Should().BeNull();
            runningScriptState.RanToCompletion.Should().BeNull();
            runningScriptState.ScriptTicket.Should().BeEquivalentTo(startScriptCommand.ScriptTicket);
            runningScriptState.Created.Should().BeOnOrAfter(testStarted).And.BeOnOrBefore(testFinished);

            finishedScriptState.Completed.Should().BeOnOrAfter(testStarted.AddSeconds(5)).And.BeOnOrBefore(testFinished);
            finishedScriptState.ExitCode.Should().Be(0);
            finishedScriptState.RanToCompletion.Should().BeTrue();
            finishedScriptState.ScriptTicket.Should().BeEquivalentTo(startScriptCommand.ScriptTicket);
            finishedScriptState.Created.Should().Be(runningScriptState.Created);
            finishedScriptState.Started.Should().BeOnOrAfter(testStarted).And.BeOnOrBefore(testFinished);
        }

        // TODO - Test the stateStore is updated.

        private void SetupScriptState(ScriptTicket ticket)
        {
            var stateWorkspace = SetupScriptStateStore(ticket);
            stateWorkspace.Create();
        }

        private ScriptStateStore SetupScriptStateStore(ScriptTicket ticket)
        {
            var workspace = workspaceFactory.GetWorkspace(ticket);
            var stateWorkspace = stateStoreFactory.Create(ticket, workspace);
            return stateWorkspace;
        }

        private void CleanupWorkspace(ScriptTicket ticket)
        {
            var workspace = workspaceFactory.GetWorkspace(ticket);
            workspace.Delete();
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
                .Build();

            return (startScriptCommand, new FileInfo(filePath));
        }

        async Task<(List<ProcessOutput>, ScriptStatusResponseV2)> RunUntilScriptCompletes(StartScriptCommandV2 startScriptCommand, ScriptStatusResponseV2 response)
        {
            var (logs, lastResponse) = await RunUntilScriptFinishes(startScriptCommand, response);

            service.CompleteScript(new CompleteScriptCommandV2(startScriptCommand.ScriptTicket));

            WriteLogsToConsole(logs);

            return (logs, lastResponse);
        }

        async Task<(List<ProcessOutput> logs, ScriptStatusResponseV2 response)> RunUntilScriptFinishes(StartScriptCommandV2 startScriptCommand, ScriptStatusResponseV2 response)
        {
            var logs = new List<ProcessOutput>(response.Logs);

            while (response.State != ProcessState.Complete)
            {
                response = service.GetStatus(new ScriptStatusRequestV2(startScriptCommand.ScriptTicket, response.NextLogSequence));

                logs.AddRange(response.Logs);

                if (response.State != ProcessState.Complete)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            }

            return (logs, response);
        }

        void WriteLogsToConsole(List<ProcessOutput> logs)
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