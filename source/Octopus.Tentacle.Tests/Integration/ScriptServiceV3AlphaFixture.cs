using System;
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
using Octopus.Tentacle.Contracts.Builders;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Services.Scripts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration
{
    [TestFixture]
    public class ScriptServiceV3AlphaFixture
    {
        IAsyncScriptServiceV3Alpha service = null!;
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

            var localShellScriptExecutor = new LocalShellScriptExecutor(
                PlatformDetection.IsRunningOnWindows ? new PowerShell() : new Bash(),
                Substitute.For<ISystemLog>());

            service = new ScriptServiceV3Alpha(
                localShellScriptExecutor,
                workspaceFactory,
                stateStoreFactory);
        }

        [Test]
        public async Task ShouldExecuteAScriptSuccessfully()
        {
            const string windowsScript = "& ping.exe localhost -n 1";
            const string bashScript = "ping localhost -c 1";

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                .Build();

            var startScriptResponse = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);
            var (logs, finalResponse) = await RunUntilScriptCompletes(startScriptCommand, startScriptResponse);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);
            logs.Count.Should().BeGreaterThan(1);
        }

        [Test]
        public async Task ShouldReturnANonZeroExitCodeForAFailingScript()
        {
            const string windowsScript = "& ping.exe nope -n 1";
            const string bashScript = "ping nope -c 1";

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                .Build();

            var startScriptResponse = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);
            var (logs, finalResponse) = await RunUntilScriptCompletes(startScriptCommand, startScriptResponse);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().NotBe(0);
            logs.Count.Should().BeGreaterThan(1);
        }

        [Test]
        public async Task ShouldExecuteMultipleScriptsConcurrently()
        {
            const string bashScript = "sleep 10";
            const string windowsScript = "Start-Sleep -Seconds 10";

            var scripts = Enumerable.Range(0, 5).Select(x =>
                new StartScriptCommandAndResponse(command: new StartScriptCommandV3AlphaBuilder()
                    .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                    .Build())).ToList();

            var started = Stopwatch.StartNew();

            var tasks = scripts.Select(async script =>
            {
                script.Response = await service.StartScriptAsync(script.Command, CancellationToken.None);
            });

            await Task.WhenAll(tasks);

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

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBody(script)
                .Build();

            var durationUntilScriptStartedRunning = Stopwatch.StartNew();
            var response = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);

            while (response.State != ProcessState.Complete)
            {
                response = await service.GetStatusAsync(new ScriptStatusRequestV3Alpha(startScriptCommand.ScriptTicket, response.NextLogSequence), CancellationToken.None);

                if (response.State == ProcessState.Running && durationUntilScriptStartedRunning.IsRunning)
                {
                    durationUntilScriptStartedRunning.Stop();
                }

                if (response.State != ProcessState.Complete)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            }

            await service.CompleteScriptAsync(new CompleteScriptCommandV3Alpha(startScriptCommand.ScriptTicket), CancellationToken.None);

            durationUntilScriptStartedRunning.Elapsed.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(10));
        }

        [Test]
        public async Task ShouldExecuteALongRunningScriptSuccessfully()
        {
            var bashScript = "sleep 10";
            var windowsScript = "Start-Sleep -Seconds 10";

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                .Build();

            var startScriptResponse = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);
            var (_, finalResponse) = await RunUntilScriptCompletes(startScriptCommand, startScriptResponse);

            finalResponse.State.Should().Be(ProcessState.Complete);
            finalResponse.ExitCode.Should().Be(0);
        }

        [Test]
        public async Task StartScriptShouldWaitForAShortScriptToFinish()
        {
            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBody("echo \"finished\"")
                .WithDurationStartScriptCanWaitForScriptToFinish(TimeSpan.FromSeconds(5))
                .Build();

            var startScriptResponse = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);

            startScriptResponse.State.Should().Be(ProcessState.Complete);
            startScriptResponse.ExitCode.Should().Be(0);
            startScriptResponse.Logs.Count.Should().BeGreaterThan(1);

            await service.CompleteScriptAsync(new CompleteScriptCommandV3Alpha(startScriptCommand.ScriptTicket), CancellationToken.None);
        }

        [Test]
        public async Task StartScriptShouldNotWaitForALongScriptToFinish()
        {
            var bashScript = "sleep 10";
            var windowsScript = "Start-Sleep -Seconds 10";

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                .WithDurationStartScriptCanWaitForScriptToFinish(TimeSpan.FromSeconds(5))
                .Build();

            var startScriptResponse = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);
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
            await service.StartScriptAsync(script1.StartScriptCommand, CancellationToken.None);

            var startScriptResponse = await service.StartScriptAsync(script2.StartScriptCommand, CancellationToken.None);
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

            var scripts = new List<(StartScriptCommandV3Alpha StartScriptCommand, FileInfo FileScriptWillCreate)>();

            for (var i = 0; i < 1000; i++)
            {
                scripts.Add(GetStartScriptCommandForScriptThatCreatesAFile(scriptTicket, scriptDelayInSeconds: 10));
            }

            // Act
            var tasks = scripts.Select(x => service.StartScriptAsync(x.StartScriptCommand, CancellationToken.None));

            await Task.WhenAll(tasks);

            var (_, finalResponse) = await RunUntilScriptCompletes(
                scripts[0].StartScriptCommand,
                new ScriptStatusResponseV3Alpha(scripts[0].StartScriptCommand.ScriptTicket, ProcessState.Pending, 0, new List<ProcessOutput>(), 0));

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

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                .Build();

            var cancellationTimer = new Stopwatch();
            var response = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);
            var logs = new List<ProcessOutput>(response.Logs);

            while (response.State != ProcessState.Complete)
            {
                response = await service.GetStatusAsync(new ScriptStatusRequestV3Alpha(startScriptCommand.ScriptTicket, response.NextLogSequence), CancellationToken.None);
                logs.AddRange(response.Logs);

                if (response.State == ProcessState.Running && !cancellationTimer.IsRunning)
                {
                    cancellationTimer.Start();
                    response = await service.CancelScriptAsync(new CancelScriptCommandV3Alpha(startScriptCommand.ScriptTicket, response.NextLogSequence), CancellationToken.None);
                    logs.AddRange(response.Logs);
                }

                if (response.State != ProcessState.Complete)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            }

            cancellationTimer.Stop();

            await service.CompleteScriptAsync(new CompleteScriptCommandV3Alpha(startScriptCommand.ScriptTicket), CancellationToken.None);

            WriteLogsToConsole(logs);

            response.State.Should().Be(ProcessState.Complete);
            response.ExitCode.Should().NotBe(0, "The exit code varies based on the OS and flakiness with killing the running script");
            cancellationTimer.Elapsed.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(5));
        }

        [Test]
        public async Task CompleteScriptShouldCleanupTheWorkspace()
        {
            var script = "echo \"finished\"";

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBody(script)
                .Build();

            var workspaceDirectory = workspaceFactory.GetWorkingDirectoryPath(startScriptCommand.ScriptTicket);
            Directory.Exists(workspaceDirectory).Should().BeFalse();

            var startScriptResponse = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);
            Directory.Exists(workspaceDirectory).Should().BeTrue();

            await RunUntilScriptCompletes(startScriptCommand, startScriptResponse);
            Directory.Exists(workspaceDirectory).Should().BeFalse();
        }

        [Test]
        public async Task GetStatusShouldReturnAnExitCodeOf45ForAnUnknownScriptTicket()
        {
            var response = await service.GetStatusAsync(new ScriptStatusRequestV3Alpha(new ScriptTicket("nope"), 0), CancellationToken.None);

            response.ExitCode.Should().Be(-45);
        }

        [Test]
        public async Task CancelScriptShouldReturnAnExitCodeOf45ForAnUnknownScriptTicket()
        {
            var response = await service.CancelScriptAsync(new CancelScriptCommandV3Alpha(new ScriptTicket("nope"), 0), CancellationToken.None);

            response.ExitCode.Should().Be(-45);
        }

        [Test]
        public async Task CompleteScriptShouldNotErrorForAnUnknownScriptTicket()
        {
            await service.CompleteScriptAsync(new CompleteScriptCommandV3Alpha(new ScriptTicket("nope")), CancellationToken.None);
        }

        [Test]
        public async Task GetStatusShouldReturnAnExitCodeOf46ForAScriptWithAnUnknownResult()
        {
            var request = new ScriptStatusRequestV3Alpha(new ScriptTicket($"did-not-finish-{Guid.NewGuid()}"), 0);
            var ticket = request.ScriptTicket;
            SetupScriptState(ticket);

            var response = await service.GetStatusAsync(request, CancellationToken.None);

            response.ExitCode.Should().Be(-46);

            await CleanupWorkspace(ticket, CancellationToken.None);
        }

        [Test]
        public async Task CancelScriptShouldReturnAnExitCodeOf46ForAScriptWithAnUnknownResult()
        {
            var request = new CancelScriptCommandV3Alpha(new ScriptTicket($"did-not-finish-{Guid.NewGuid()}"), 0);
            var ticket = request.ScriptTicket;
            SetupScriptState(ticket);

            var response = await service.CancelScriptAsync(request, CancellationToken.None);

            response.ExitCode.Should().Be(-46);

            await CleanupWorkspace(ticket, CancellationToken.None);
        }

        [Test]
        public async Task CompleteScriptShouldNotErrorForAScriptWithAnUnknownResult()
        {
            var request = new CompleteScriptCommandV3Alpha(new ScriptTicket($"did-not-finish-{Guid.NewGuid()}"));
            var ticket = request.ScriptTicket;
            SetupScriptState(ticket);

            await service.CompleteScriptAsync(request, CancellationToken.None);
        }

        [Test]
        public async Task ShouldStoreTheStateOfTheScriptInTheScriptStateStore()
        {
            var testStarted = DateTimeOffset.UtcNow;

            var bashScript = "sleep 10";
            var windowsScript = "Start-Sleep -Seconds 10";

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                .Build();

            var scriptStateStore = SetupScriptStateStore(startScriptCommand.ScriptTicket);

            var startScriptResponse = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);

            var runningScriptState = scriptStateStore.Load();

            var (logs, finalResponse) = await RunUntilScriptFinishes(startScriptCommand, startScriptResponse);

            var testFinished = DateTimeOffset.UtcNow;
            var finishedScriptState = scriptStateStore.Load();

            await service.CompleteScriptAsync(new CompleteScriptCommandV3Alpha(startScriptCommand.ScriptTicket), CancellationToken.None);
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
            // Arrange
            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBody("echo \"finished\"")
                .Build();

            var response = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);

            var upperCaseScriptTicket = new ScriptTicket(startScriptCommand.ScriptTicket.TaskId.ToUpper());
            var lowerCaseScriptTicket = new ScriptTicket(startScriptCommand.ScriptTicket.TaskId.ToLower());

            // Act
            var upperCaseResponse = await service.GetStatusAsync(new ScriptStatusRequestV3Alpha(upperCaseScriptTicket, response.NextLogSequence), CancellationToken.None);
            var lowerCaseResponse = await service.GetStatusAsync(new ScriptStatusRequestV3Alpha(lowerCaseScriptTicket, response.NextLogSequence), CancellationToken.None);
            await service.CompleteScriptAsync(new CompleteScriptCommandV3Alpha(startScriptCommand.ScriptTicket), CancellationToken.None);

            // Assert
            upperCaseResponse.ExitCode.Should().Be(0);
            lowerCaseResponse.ExitCode.Should().Be(0);
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
            var stateWorkspace = stateStoreFactory.Create(workspace);
            return stateWorkspace;
        }

        private async Task CleanupWorkspace(ScriptTicket ticket, CancellationToken cancellationToken)
        {
            var workspace = workspaceFactory.GetWorkspace(ticket);
            await workspace.Delete(cancellationToken);
        }

        (StartScriptCommandV3Alpha StartScriptCommand, FileInfo FileScriptWillCreate) GetStartScriptCommandForScriptThatCreatesAFile(ScriptTicket scriptTicket, int? scriptDelayInSeconds = null)
        {
            var filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            var bashScript = @$"echo ""Started 1"" > ""{filePath}""";
            var windowsScript = @$"echo ""Started 1"" > ""{filePath}""";

            if (scriptDelayInSeconds != null)
            {
                bashScript += $"{Environment.NewLine} sleep {scriptDelayInSeconds}";
                windowsScript += $"{Environment.NewLine} Start-Sleep -Seconds {scriptDelayInSeconds}";
            }

            var startScriptCommand = new StartScriptCommandV3AlphaBuilder()
                .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                .WithScriptTicket(scriptTicket)
                .Build();

            return (startScriptCommand, new FileInfo(filePath));
        }

        async Task<(List<ProcessOutput>, ScriptStatusResponseV3Alpha)> RunUntilScriptCompletes(StartScriptCommandV3Alpha startScriptCommand, ScriptStatusResponseV3Alpha response)
        {
            var (logs, lastResponse) = await RunUntilScriptFinishes(startScriptCommand, response);

            await service.CompleteScriptAsync(new CompleteScriptCommandV3Alpha(startScriptCommand.ScriptTicket), CancellationToken.None);

            WriteLogsToConsole(logs);

            return (logs, lastResponse);
        }

        async Task<(List<ProcessOutput> logs, ScriptStatusResponseV3Alpha response)> RunUntilScriptFinishes(StartScriptCommandV3Alpha startScriptCommand, ScriptStatusResponseV3Alpha response)
        {
            var logs = new List<ProcessOutput>(response.Logs);

            while (response.State != ProcessState.Complete)
            {
                response = await service.GetStatusAsync(new ScriptStatusRequestV3Alpha(startScriptCommand.ScriptTicket, response.NextLogSequence), CancellationToken.None);

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
            public StartScriptCommandAndResponse(StartScriptCommandV3Alpha command)
            {
                Command = command;
            }

            public StartScriptCommandV3Alpha Command { get; }
            public ScriptStatusResponseV3Alpha? Response { get; set; }
        }
    }
}