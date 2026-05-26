using System;
using System.IO;
using System.Linq;
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
        public async Task AbandonScript_WhenCancelFailsToKillProcess_ReturnsAbandonedExitCode(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            // TentacleDebugDisableProcessKill=1 makes Hitman a no-op, so CancelScript cannot
            // actually terminate the underlying script process. The script becomes genuinely
            // "stuck" from Tentacle's perspective. AbandonScript should then return promptly
            // with AbandonedExitCode without waiting for the process to exit.
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(x => x.WithRunTentacleEnvironmentVariable(EnvironmentVariables.TentacleDebugDisableProcessKill, "1"))
                .Build(CancellationToken);

            var startFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "start");
            var releaseFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "release");

            var firstCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder()
                    .CreateFile(startFile)
                    .WaitForFileToExist(releaseFile))
                .WithIsolationLevel(ScriptIsolationLevel.NoIsolation)
                .Build();

            var tentacleClient = clientTentacle.TentacleClient;
            var scriptServiceV2 = clientTentacle.CreateScriptServiceV2Client();

            var scriptExecution = Task.Run(async () => await tentacleClient.ExecuteScript(firstCommand, CancellationToken));

            await Wait.For(() => File.Exists(startFile),
                TimeSpan.FromSeconds(30),
                () => throw new Exception("Script did not start"),
                CancellationToken);

            // Cancel: Hitman is a no-op so the process keeps running.
            await scriptServiceV2.CancelScriptAsync(
                new CancelScriptCommandV2(firstCommand.ScriptTicket, 0),
                new HalibutProxyRequestOptions(CancellationToken));
            await Task.Delay(TimeSpan.FromSeconds(1));

            // Abandon: fires the abandon token. The RPC returns the current status snapshot
            // immediately, so we poll GetStatus until the script reaches Complete state.
            await scriptServiceV2.AbandonScriptAsync(
                new AbandonScriptCommandV2(firstCommand.ScriptTicket, 0),
                new HalibutProxyRequestOptions(CancellationToken));

            ScriptStatusResponseV2 abandonResponse = null!;
            await Wait.For(async () =>
                {
                    abandonResponse = await scriptServiceV2.GetStatusAsync(
                        new ScriptStatusRequestV2(firstCommand.ScriptTicket, 0),
                        new HalibutProxyRequestOptions(CancellationToken));
                    return abandonResponse.State == ProcessState.Complete;
                },
                TimeSpan.FromSeconds(30),
                () => throw new Exception("Abandoned script did not reach Complete state within 30s"),
                CancellationToken);
            abandonResponse.ExitCode.Should().Be(ScriptExitCodes.AbandonedExitCode);

            // Release the script process so it exits cleanly and stops leaking.
            File.WriteAllText(releaseFile, "");
            await scriptExecution;
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.Version2)]
        public async Task AbandonScript_ReleasesIsolationMutexEvenWhileProcessIsStillRunning(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            // The whole reason Tentacle needs an abandon RPC is to release the isolation mutex
            // when CancelScript can't unstick the script. This test proves that contract: a
            // FullIsolation script gets stuck (because TentacleDebugDisableProcessKill makes
            // cancel a no-op), abandon is called, and a second FullIsolation script with the
            // same mutex name must then be able to acquire the mutex and run.
            await using var clientTentacle = await tentacleConfigurationTestCase.CreateBuilder()
                .WithTentacle(x => x.WithRunTentacleEnvironmentVariable(EnvironmentVariables.TentacleDebugDisableProcessKill, "1"))
                .Build(CancellationToken);

            var startFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "start");
            var releaseFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "release");

            const string sharedMutex = "abandon-test-mutex";

            var firstCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder()
                    .CreateFile(startFile)
                    .WaitForFileToExist(releaseFile))
                .WithIsolationLevel(ScriptIsolationLevel.FullIsolation)
                .WithIsolationMutexName(sharedMutex)
                .Build();

            var tentacleClient = clientTentacle.TentacleClient;
            var scriptServiceV2 = clientTentacle.CreateScriptServiceV2Client();

            var firstScriptExecution = Task.Run(async () => await tentacleClient.ExecuteScript(firstCommand, CancellationToken));

            await Wait.For(() => File.Exists(startFile),
                TimeSpan.FromSeconds(30),
                () => throw new Exception("First script did not start"),
                CancellationToken);

            await scriptServiceV2.CancelScriptAsync(
                new CancelScriptCommandV2(firstCommand.ScriptTicket, 0),
                new HalibutProxyRequestOptions(CancellationToken));
            await Task.Delay(TimeSpan.FromSeconds(1));

            await scriptServiceV2.AbandonScriptAsync(
                new AbandonScriptCommandV2(firstCommand.ScriptTicket, 0),
                new HalibutProxyRequestOptions(CancellationToken));

            // Second FullIsolation script with the SAME mutex name. If the abandon released
            // the mutex, this script can acquire it and run to completion. Otherwise it would
            // block waiting for the (still-alive) first script's mutex hold.
            var secondStartFile = Path.Combine(clientTentacle.TemporaryDirectory.DirectoryPath, "second-start");
            var secondCommand = new TestExecuteShellScriptCommandBuilder()
                .SetScriptBody(new ScriptBuilder().CreateFile(secondStartFile))
                .WithIsolationLevel(ScriptIsolationLevel.FullIsolation)
                .WithIsolationMutexName(sharedMutex)
                .Build();

            var (secondResult, _) = await tentacleClient.ExecuteScript(secondCommand, CancellationToken);
            secondResult.ExitCode.Should().Be(0);
            File.Exists(secondStartFile).Should().BeTrue("second script should have run after the mutex was released");

            File.WriteAllText(releaseFile, "");
            await firstScriptExecution;
        }
    }
}
