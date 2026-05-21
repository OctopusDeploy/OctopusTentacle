using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.ServiceModel;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Core.Util;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Tests.Integration.Util.Builders;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ClientScriptExecutionAbandon : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.Version2)]
        public async Task AbandonScript_WhileScriptIsRunning_ReleasesMutexAndReturnsAbandonedExitCode(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(x => x.WithRunTentacleEnvironmentVariable(EnvironmentVariables.TentacleDebugDisableProcessKill, "1"))
                .Build(CancellationToken);

            var startFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "start");
            var releaseFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "release");

            var firstCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder()
                    .CreateFile(startFile)
                    .WaitForFileToExist(releaseFile))
                .WithIsolationLevel(ScriptIsolationLevel.FullIsolation)
                .WithIsolationMutexName("abandon-test-mutex")
                .Build();

            var tentacleClient = clientTentacle.TentacleClient;
            var scriptServiceV2 = clientTentacle.CreateScriptServiceV2Client();

            var firstScriptExecution = Task.Run(async () => await tentacleClient.ExecuteScript(firstCommand, CancellationToken));

            // Wait for the first script to start running
            await Wait.For(() => File.Exists(startFile),
                TimeSpan.FromSeconds(30),
                () => throw new Exception("First script did not start"),
                CancellationToken);
            Logger.Information("First script is now running, calling CancelScript then AbandonScript");

            // Cancel the script — with TentacleDebugDisableProcessKill=1 the process is not killed
            await scriptServiceV2.CancelScriptAsync(
                new CancelScriptCommandV2(firstCommand.ScriptTicket, 0),
                new HalibutProxyRequestOptions(CancellationToken));

            // Give the cancel a moment to be processed before abandoning
            await Task.Delay(TimeSpan.FromSeconds(1));

            // Abandon fires the abandon token, which causes SilentProcessRunner to return AbandonedExitCode
            // without killing the process, and releases the isolation mutex
            await scriptServiceV2.AbandonScriptAsync(
                new AbandonScriptCommandV2(firstCommand.ScriptTicket, 0),
                new HalibutProxyRequestOptions(CancellationToken));

            // Load-bearing: second FullIsolation script with the same mutex name must now be able to start,
            // proving the mutex was released by the abandon
            var secondStartFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "second-start");
            var secondCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder().CreateFile(secondStartFile))
                .WithIsolationLevel(ScriptIsolationLevel.FullIsolation)
                .WithIsolationMutexName("abandon-test-mutex")
                .Build();

            var (secondResult, _) = await tentacleClient.ExecuteScript(secondCommand, CancellationToken);
            secondResult.ExitCode.Should().Be(0);
            File.Exists(secondStartFile).Should().BeTrue("second script should have run after the mutex was released");

            // Release the first script's file-wait so the underlying process exits cleanly
            File.WriteAllText(releaseFile, "");

            // Allow the first script execution task to complete
            await firstScriptExecution;
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.Version2)]
        public async Task AbandonScript_MultiLevelDeepHang_StillReleasesMutex(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(x => x.WithRunTentacleEnvironmentVariable(EnvironmentVariables.TentacleDebugDisableProcessKill, "1"))
                .Build(CancellationToken);

            var startFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "ml-start");
            var releaseFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "ml-release");

            // Multi-level chain: outer shell (bootstrap) → child shell → grandchild polls for the release file.
            // This mirrors a bootstrap → Calamari → user script hierarchy where the stuck process is NOT
            // the direct child of Tentacle.
            var releaseFileForwardSlash = releaseFile.Replace("\\", "/");
            var script = new ScriptBuilder()
                .CreateFile(startFile)
                .AppendRaw(
                    bash: $"bash -c \"bash -c 'while [ ! -f {releaseFileForwardSlash} ]; do sleep 0.5; done'\"",
                    windows: $"powershell -NoProfile -Command \"powershell -NoProfile -Command 'while (-not (Test-Path \\\"{releaseFile}\\\")) {{ Start-Sleep -Milliseconds 500 }}'\"");

            var firstCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(script)
                .WithIsolationLevel(ScriptIsolationLevel.FullIsolation)
                .WithIsolationMutexName("abandon-multilevel-mutex")
                .Build();

            var tentacleClient = clientTentacle.TentacleClient;
            var scriptServiceV2 = clientTentacle.CreateScriptServiceV2Client();

            var firstScriptExecution = Task.Run(async () => await tentacleClient.ExecuteScript(firstCommand, CancellationToken));

            // Wait for the first script to start running
            await Wait.For(() => File.Exists(startFile),
                TimeSpan.FromSeconds(30),
                () => throw new Exception("Multi-level script did not start"),
                CancellationToken);
            Logger.Information("Multi-level script is now running, calling CancelScript then AbandonScript");

            // Cancel the script — with TentacleDebugDisableProcessKill=1 the process tree is not killed
            await scriptServiceV2.CancelScriptAsync(
                new CancelScriptCommandV2(firstCommand.ScriptTicket, 0),
                new HalibutProxyRequestOptions(CancellationToken));

            // Give the cancel a moment to be processed before abandoning
            await Task.Delay(TimeSpan.FromSeconds(1));

            // Abandon fires the abandon token, which causes SilentProcessRunner to return AbandonedExitCode
            // without killing the process, and releases the isolation mutex
            var abandonResponse = await scriptServiceV2.AbandonScriptAsync(
                new AbandonScriptCommandV2(firstCommand.ScriptTicket, 0),
                new HalibutProxyRequestOptions(CancellationToken));

            abandonResponse.ExitCode.Should().Be(ScriptExitCodes.AbandonedExitCode);

            // Load-bearing: second FullIsolation script with the same mutex name must now be able to start,
            // proving the mutex was released by the abandon even when the stuck process is not a direct child
            var secondStartFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "ml-second-start");
            var secondCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder().CreateFile(secondStartFile))
                .WithIsolationLevel(ScriptIsolationLevel.FullIsolation)
                .WithIsolationMutexName("abandon-multilevel-mutex")
                .Build();

            var (secondResult, _) = await tentacleClient.ExecuteScript(secondCommand, CancellationToken);
            secondResult.ExitCode.Should().Be(0);
            File.Exists(secondStartFile).Should().BeTrue("second script should have run after the mutex was released");

            // Release the deepest grandchild so the process tree eventually exits cleanly
            File.WriteAllText(releaseFile, "");

            // Allow the first script execution task to complete
            await firstScriptExecution;
        }
    }
}
