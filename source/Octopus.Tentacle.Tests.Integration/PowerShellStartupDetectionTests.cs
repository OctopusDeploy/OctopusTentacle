using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.CommonTestUtils.Diagnostics;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Core.Services.Scripts;
using Octopus.Tentacle.Core.Services.Scripts.PowerShellStartup;
using Octopus.Tentacle.Core.Services.Scripts.Locking;
using Octopus.Tentacle.Core.Services.Scripts.Security.Masking;
using Octopus.Tentacle.Core.Services.Scripts.Shell;
using Octopus.Tentacle.Core.Services.Scripts.StateStore;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class PowerShellStartupDetectionTests : IntegrationTest
    {
        static (ScriptServiceV2 service, ScriptWorkspaceFactory workspaceFactory, ScriptStateStoreFactory stateStoreFactory, TemporaryDirectory tempDir) CreateScriptService(
            TimeSpan? powerShellStartupTimeout = null)
        {
            var tempDir = new TemporaryDirectory();
            
            var systemLog = new SerilogSystemLog(new SerilogLoggerBuilder().Build());
            
            var homeConfiguration = Substitute.For<IHomeConfiguration>();
            homeConfiguration.HomeDirectory.Returns(tempDir.DirectoryPath);

            var octopusPhysicalFileSystem = new OctopusPhysicalFileSystem(systemLog);
            var workspaceFactory = new ScriptWorkspaceFactory(octopusPhysicalFileSystem, homeConfiguration, new SensitiveValueMasker(), 
                useBashWorkspace: false // Force the powershell workspace to be used
                );
            var stateStoreFactory = new ScriptStateStoreFactory(octopusPhysicalFileSystem);
            
            var shell = GetShellForCurrentPlatform();
            
            var service = new ScriptServiceV2(
                shell,
                workspaceFactory,
                stateStoreFactory,
                new ScriptIsolationMutex(),
                systemLog,
                new Dictionary<string, string>(),
                powerShellStartupTimeout ?? PowerShellStartupDetection.PowerShellStartupTimeout);
            
            
            return (service, workspaceFactory, stateStoreFactory, tempDir);
        }

        [Test]
        public async Task WhenPowerShellScriptHasDetectionComment_AndRunsSuccessfully_ScriptSucceeds()
        {
            var (service, _, _, tempDir) = CreateScriptService(powerShellStartupTimeout: TimeSpan.FromSeconds(60));
            using (tempDir)
            {
                var scriptBody = @"
# TENTACLE-POWERSHELL-STARTUP-DETECTION
write-output 'Hello from PowerShell'
write-output 'Script completed successfully'
";

                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(scriptBody)
                    .WithIsolation(ScriptIsolationLevel.NoIsolation)
                    .Build();

                var startScriptResponse = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);
                var (logs, finalResponse) = await RunUntilScriptCompletes(service, startScriptCommand, startScriptResponse);

                finalResponse.State.Should().Be(ProcessState.Complete);
                finalResponse.ExitCode.Should().Be(0);

                var allLogs = string.Join("\n", logs.Select(l => l.Text));
                allLogs.Should().Contain("Hello from PowerShell");
                allLogs.Should().Contain("Script completed successfully");
            }
        }
        
        
        [Test]
        public async Task WhenPowerShellScriptHasDetectionComment_AndPowershellScriptRunsLongerThanThePowerShellStartupTimeout_ScriptSucceeds()
        {
            var (service, _, _, tempDir) = CreateScriptService(powerShellStartupTimeout: TimeSpan.FromSeconds(10));
            using (tempDir)
            {
                var scriptBody = @"
# TENTACLE-POWERSHELL-STARTUP-DETECTION
Start-Sleep -Seconds 20
write-output 'Hello from PowerShell'
write-output 'Script completed successfully'
";

                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(scriptBody)
                    .WithIsolation(ScriptIsolationLevel.NoIsolation)
                    .Build();

                var startScriptResponse = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);
                var (logs, finalResponse) = await RunUntilScriptCompletes(service, startScriptCommand, startScriptResponse);

                finalResponse.State.Should().Be(ProcessState.Complete);
                finalResponse.ExitCode.Should().Be(0);

                var allLogs = string.Join("\n", logs.Select(l => l.Text));
                allLogs.Should().Contain("Hello from PowerShell");
                allLogs.Should().Contain("Script completed successfully");
            }
        }

        [Test]
        public async Task WhenPowerShellNeverStarts_DetectionReportsFailure()
        {
            var (service, _, _, tempDir) = CreateScriptService(powerShellStartupTimeout: TimeSpan.FromSeconds(2));
            using (tempDir)
            {
                // Simulate PowerShell hanging before the detection code by sleeping for a long time
                // This tests the scenario where PowerShell.exe starts but hangs before executing our script
                var scriptBody = $@"
# Sleep for a long time to simulate PowerShell hanging before reaching detection code
Start-Sleep -Seconds 3600
# TENTACLE-POWERSHELL-STARTUP-DETECTION
write-output 'This should never be printed'
";

                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(scriptBody)
                    .Build();

                var startScriptResponse = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);
                var (logs, finalResponse) = await RunUntilScriptCompletes(service, startScriptCommand, startScriptResponse);

                finalResponse.State.Should().Be(ProcessState.Complete);
                finalResponse.ExitCode.Should().Be(ScriptExitCodes.PowerShellNeverStartedExitCode);

                var allLogs = string.Join("\n", logs.Select(l => l.Text));
                allLogs.Should().Contain("process did not start within");
            }
        }
        
        [Test]
        public async Task WhenPowerShellNeverStarts_WeShouldDetectTheScriptDidNotStart_AndAttemptToCancelTheScript()
        {
            var (service, _, _, tempDir) = CreateScriptService(powerShellStartupTimeout: TimeSpan.FromSeconds(2));
            using (tempDir)
            {
                var stillRunning = Path.Combine(tempDir.DirectoryPath, "stillRunning");
                // Simulate PowerShell hanging before the detection code by sleeping for a long time
                // This tests the scenario where PowerShell.exe starts but hangs before executing our script
                var scriptBody = $@"
while ($true) {{
    Add-Content -Path '{stillRunning}' -Value 'This is the appended text.'
    Start-Sleep -Seconds 1
}}
# TENTACLE-POWERSHELL-STARTUP-DETECTION
write-output 'This should never be printed'
";

                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(scriptBody)
                    .Build();

                var startScriptResponse = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);
                var (logs, finalResponse) = await RunUntilScriptCompletes(service, startScriptCommand, startScriptResponse);

                finalResponse.State.Should().Be(ProcessState.Complete);
                finalResponse.ExitCode.Should().Be(ScriptExitCodes.PowerShellNeverStartedExitCode);

                await DeletePotentiallyInUseFile(stillRunning);
                await Task.Delay(TimeSpan.FromSeconds(5));
                File.Exists(stillRunning).Should().BeFalse("Otherwise the script is still running and we made not effort to cancel it.");
            }
        }

        async Task DeletePotentiallyInUseFile(string file)
        {
            while (true)
            {
                this.CancellationToken.ThrowIfCancellationRequested();
                try
                {
                    File.Delete(file);
                    break;
                }
                catch (Exception) { }
                Logger.Information("Will re-attempt to delete {file} in 100ms", file);
                await Task.Delay(TimeSpan.FromMilliseconds(100), this.CancellationToken);
            }
        }

        [Test]
        public async Task WhenPowerShellScriptWithoutDetectionComment_NormalExecutionOccurs()
        {
            var (service, _, _, tempDir) = CreateScriptService(powerShellStartupTimeout: TimeSpan.FromSeconds(2));
            using (tempDir)
            {
                // If we have a long-running script that does not have the detection comment,
                // then tentacle should not bother with any detection logic. This includes not terminating the script
                // because it never reported as running.
                var scriptBody = @"
Start-Sleep -Seconds 10
write-output 'Hello from PowerShell without detection'
write-output 'Script completed successfully'";

                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(scriptBody)
                    .WithIsolation(ScriptIsolationLevel.NoIsolation)
                    .WithDurationStartScriptCanWaitForScriptToFinish(null)
                    .Build();

                var startScriptResponse = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);
                var (logs, finalResponse) = await RunUntilScriptCompletes(service, startScriptCommand, startScriptResponse);

                finalResponse.State.Should().Be(ProcessState.Complete);
                finalResponse.ExitCode.Should().Be(0);

                var allLogs = string.Join("\n", logs.Select(l => l.Text));
                
                allLogs.Should().NotContain("PowerShell startup detection");
                // PowerShell output might not be captured in all test environments
                // The important thing is that it completes successfully
            }
        }
        
        
        [Test]
        public async Task WhenPowerShellNeverStarts_WeShouldDetectTheScriptDidNotStart_AndTheScriptShouldNotBeAbleToStartAgain()
        {
            var (service, workspaceFactory, _, tempDir) = CreateScriptService(powerShellStartupTimeout: TimeSpan.FromSeconds(2));
            using (tempDir)
            {
                var shouldSleep = Path.Combine(tempDir.DirectoryPath, "shouldSleep");
                File.WriteAllText(shouldSleep, "");

                var scriptBody = $@"
while (Test-Path -Path '{shouldSleep}') {{
    Start-Sleep -Seconds 1
}}
# TENTACLE-POWERSHELL-STARTUP-DETECTION
write-output 'This should never be printed'
";

                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(scriptBody)
                    .Build();

                var startScriptResponse = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);
                var (_, finalResponse) = await RunUntilScriptCompletes(service, startScriptCommand, startScriptResponse);

                finalResponse.State.Should().Be(ProcessState.Complete);
                finalResponse.ExitCode.Should().Be(ScriptExitCodes.PowerShellNeverStartedExitCode);

                // At this point the monitor has:
                // - Created the "started" file (prevents PowerShell from creating it)
                // - Deleted the "should-run" file (prevents PowerShell from running even if workspace is cleaned up)

                // Delete shouldSleep so the script can proceed past the loop when re-invoked directly
                File.Delete(shouldSleep);

                // Re-invoke the bootstrap script directly - the detection code should block it from running
                var workspace = workspaceFactory.GetWorkspace(startScriptCommand.ScriptTicket, WorkspaceReadinessCheck.Skip);
                Logger.Information("Bootstrap script contents:\n{BootstrapScript}", File.ReadAllText(workspace.BootstrapScriptFilePath));
                var shell = GetShellForCurrentPlatform();
                var args = shell.FormatCommandArguments(workspace.BootstrapScriptFilePath, null, allowInteractive: false);

                var directOutput = new List<string>();
                var directExitCode = SilentProcessRunner.ExecuteCommand(
                    shell.GetFullPath(),
                    args,
                    workspace.WorkingDirectory,
                    _ => { },
                    line => directOutput.Add(line),
                    line => directOutput.Add(line),
                    CancellationToken.None);

                var directOutputText = string.Join("\n", directOutput);
                Logger.Information("Direct invocation output:\n{Output}", directOutputText);

                directOutputText.Should().Contain("PowerShell startup detection", "The detection code should have run and reported why it exited");

                // On Mac/Linux exit codes are unsigned 8-bit, so -47 wraps to 209
                if (Octopus.Tentacle.Util.PlatformDetection.IsRunningOnWindows)
                {
                    directExitCode.Should().Be(ScriptExitCodes.PowerShellNeverStartedExitCode, "The detection code should prevent the script from running");
                }
            }
        }

        [Test]
        public async Task WhenPowerShellNeverStarts_AndWorkspaceIsDeletedBeforeScriptRuns_TheScriptShouldStillNotBeAbleToStart()
        {
            var (service, workspaceFactory, _, tempDir) = CreateScriptService(powerShellStartupTimeout: TimeSpan.FromSeconds(2));
            using (tempDir)
            {
                var shouldSleep = Path.Combine(tempDir.DirectoryPath, "shouldSleep");
                File.WriteAllText(shouldSleep, "");

                var scriptBody = $@"
while (Test-Path -Path '{shouldSleep}') {{
    Start-Sleep -Seconds 1
}}
# TENTACLE-POWERSHELL-STARTUP-DETECTION
write-output 'This should never be printed'
";

                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBody(scriptBody)
                    .Build();

                var startScriptResponse = await service.StartScriptAsync(startScriptCommand, CancellationToken.None);
                var (_, finalResponse) = await RunUntilScriptCompletes(service, startScriptCommand, startScriptResponse);

                finalResponse.State.Should().Be(ProcessState.Complete);
                finalResponse.ExitCode.Should().Be(ScriptExitCodes.PowerShellNeverStartedExitCode);

                // Delete shouldSleep so the script can proceed past the loop when re-invoked directly
                File.Delete(shouldSleep);

                // Simulate the workspace being cleaned up while the script is still in memory:
                // delete every file in the workspace except the bootstrap script itself
                var workspace = workspaceFactory.GetWorkspace(startScriptCommand.ScriptTicket, WorkspaceReadinessCheck.Skip);
                var bootstrapScriptFilePath = workspace.BootstrapScriptFilePath;
                foreach (var file in Directory.GetFiles(workspace.WorkingDirectory))
                {
                    if (!string.Equals(file, bootstrapScriptFilePath, StringComparison.OrdinalIgnoreCase))
                        File.Delete(file);
                }

                Logger.Information("Bootstrap script contents:\n{BootstrapScript}", File.ReadAllText(bootstrapScriptFilePath));

                // Re-invoke the bootstrap script directly - even without the workspace files it should be blocked
                var shell = GetShellForCurrentPlatform();
                var args = shell.FormatCommandArguments(bootstrapScriptFilePath, null, allowInteractive: false);

                var directOutput = new List<string>();
                var directExitCode = SilentProcessRunner.ExecuteCommand(
                    shell.GetFullPath(),
                    args,
                    workspace.WorkingDirectory,
                    _ => { },
                    line => directOutput.Add(line),
                    line => directOutput.Add(line),
                    CancellationToken.None);

                var directOutputText = string.Join("\n", directOutput);
                Logger.Information("Direct invocation output:\n{Output}", directOutputText);

                directOutputText.Should().Contain("PowerShell startup detection", "The detection code should have run and reported why it exited");

                // On Mac/Linux exit codes are unsigned 8-bit, so -47 wraps to 209
                if (Octopus.Tentacle.Util.PlatformDetection.IsRunningOnWindows)
                {
                    directExitCode.Should().Be(ScriptExitCodes.PowerShellNeverStartedExitCode, "The detection code should prevent the script from running even when the workspace files are gone");
                }
            }
        }

        static IShell GetShellForCurrentPlatform()
        {
            if (Octopus.Tentacle.Util.PlatformDetection.IsRunningOnWindows)
            {
                return new PowerShell();
            }
            
            // On Linux/Mac, try to use pwsh (PowerShell Core)
            // First check if pwsh is available
            try
            {
                var result = SilentProcessRunner.ExecuteCommand(
                    "which",
                    "pwsh",
                    Environment.CurrentDirectory,
                    _ => { },
                    _ => { },
                    _ => { },
                    new Dictionary<string, string>(),
                    CancellationToken.None);

                if (result == 0)
                {
                    // pwsh is available, create a custom shell for it
                    return new TestPwshShell();
                }
            }
            catch
            {
                // pwsh not available
            }

            // Fall back to bash (tests will be skipped for PowerShell-specific features)
            //Assert.Inconclusive("PowerShell (pwsh) not available on this platform. Install PowerShell Core to run these tests.");
            Assert.Fail("PowerShell (pwsh) not available on this platform. Install PowerShell Core to run these tests.");
            return null!;
        }

        static async Task<(List<ProcessOutput>, ScriptStatusResponseV2)> RunUntilScriptCompletes(ScriptServiceV2 service, StartScriptCommandV2 startScriptCommand, ScriptStatusResponseV2 response)
        {
            var (logs, lastResponse) = await RunUntilScriptFinishes(service, startScriptCommand, response);
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

        static void WriteLogsToConsole(List<ProcessOutput> logs)
        {
            foreach (var log in logs)
            {
                TestContext.Out.WriteLine("{0:yyyy-MM-dd HH:mm:ss K}: {1}", log.Occurred.ToLocalTime(), log.Text);
            }
        }
    }
}
