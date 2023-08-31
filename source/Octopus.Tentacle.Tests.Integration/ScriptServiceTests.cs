using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.Support.Legacy;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class ScriptServiceTests : IntegrationTest
    {
        [Test]
        [TentacleConfigurations]
        public async Task RunScriptWithSuccess(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var windowsScript = @"
                Write-Host ""This is the start of the script""
                Write-Host ""The answer is"" (6 * 7)
                Start-Sleep -Seconds 3
                Write-Host ""This is the end of the script""";

            var nixScript = @"
                echo This is the start of the script
                val=6
                ((theAnswer=$val*7))
                echo The answer is $theAnswer
                sleep 3
                echo This is the end of the script";

            await using var clientAndTentacle = await new LegacyClientAndTentacleBuilder(tentacleConfigurationTestCase.TentacleType)
                .WithAsyncHalibutFeature(tentacleConfigurationTestCase.SyncOrAsyncHalibut.ToAsyncHalibutFeature())
                .Build(CancellationToken);

            var scriptStatusResponse = await new ScriptExecutionOrchestrator(clientAndTentacle.TentacleClient, tentacleConfigurationTestCase.SyncOrAsyncHalibut)
                .ExecuteScript(windowsScript, nixScript, CancellationToken);

            DumpLog(scriptStatusResponse);

            scriptStatusResponse.State.Should().Be(ProcessState.Complete);
            scriptStatusResponse.ExitCode.Should().Be(0);
            scriptStatusResponse.Logs.Select(x => x.Text).Should().Contain("The answer is 42");
        }

        [Test]
        [TentacleConfigurations]
        public async Task RunScriptWithErrors(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var windowsScript = @"
                Write-Host ""This is the start of the script""
                Start-Sleep -Seconds 3
                throw ""Whoopsy Daisy!""
                Write-Host ""This is the end of the script""";

            var nixScript = @"
                echo This is the start of the script
                sleep 3
                echo ""Whoopsy Daisy!""
                exit 1
                echo This is the end of the script";

            await using var clientAndTentacle = await new LegacyClientAndTentacleBuilder(tentacleConfigurationTestCase.TentacleType)
                .WithAsyncHalibutFeature(tentacleConfigurationTestCase.SyncOrAsyncHalibut.ToAsyncHalibutFeature())
                .Build(CancellationToken);

            var scriptStatusResponse = await new ScriptExecutionOrchestrator(clientAndTentacle.TentacleClient, tentacleConfigurationTestCase.SyncOrAsyncHalibut)
                .ExecuteScript(windowsScript, nixScript, CancellationToken);

            DumpLog(scriptStatusResponse);

            scriptStatusResponse.State.Should().Be(ProcessState.Complete);
            scriptStatusResponse.ExitCode.Should().NotBe(0);
            scriptStatusResponse.Logs.Select(x => x.Text).Should().Contain("Whoopsy Daisy!");
            scriptStatusResponse.Logs.Select(x => x.Text).Should().NotContain("This is the end of the script");
        }

        [Test]
        [TentacleConfigurations]
        public async Task CancelScript(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            var windowsScript = @"Write-Host ""This is the start of the script""
                                & ping.exe 127.0.0.1 -n 100
                                Write-Host ""This is the end of the script""";

            var nixScript = @"echo This is the start of the script
                              ping 127.0.0.1 -c 100
                              echo This is the end of the script";

            await using var clientAndTentacle = await new LegacyClientAndTentacleBuilder(tentacleConfigurationTestCase.TentacleType)
                .WithAsyncHalibutFeature(tentacleConfigurationTestCase.SyncOrAsyncHalibut.ToAsyncHalibutFeature())
                .Build(CancellationToken);

            var scriptExecutor = new ScriptExecutionOrchestrator(clientAndTentacle.TentacleClient, tentacleConfigurationTestCase.SyncOrAsyncHalibut);

            var ticket = await scriptExecutor.StartScript(windowsScript, nixScript, CancellationToken);

            // Possible Tentacle BUG: If we just observe until the first output is received then sometimes the script will fail to Cancel
            await scriptExecutor.ObserverUntilScriptOutputReceived(ticket, "This is the start of the script", CancellationToken);

            Console.WriteLine("Canceling");
            await tentacleConfigurationTestCase.SyncOrAsyncHalibut
                .WhenSync(() => clientAndTentacle.TentacleClient.ScriptService.SyncService.CancelScript(new CancelScriptCommand(ticket, 0)))
                .WhenAsync(async () => await clientAndTentacle.TentacleClient.ScriptService.AsyncService.CancelScriptAsync(new CancelScriptCommand(ticket, 0), new(CancellationToken, null)));
            
            var cancellationDuration = Stopwatch.StartNew();
            Console.WriteLine("Waiting for complete");
            var finalScriptStatusResponse = await scriptExecutor.ObserverUntilComplete(ticket, CancellationToken);
            cancellationDuration.Stop();

            var finalStatus = await scriptExecutor.CompleteScript(finalScriptStatusResponse, CancellationToken);
            DumpLog(finalStatus);

            finalStatus.State.Should().Be(ProcessState.Complete);
            finalStatus.ExitCode.Should().NotBe(0, "Expected ExitCode to be non-zero");
            finalStatus.Logs.Count.Should().BeGreaterThan(0, "Expected something in the logs");

            finalStatus.Logs.Select(x => x.Text).Should().Contain("This is the start of the script");
            finalStatus.Logs.Select(x => x.Text).Should().NotContain("This is the end of the script");
            cancellationDuration.Elapsed.TotalSeconds.Should().BeLessThanOrEqualTo(20);
        }

        private static void DumpLog(ScriptStatusResponse finalStatus)
        {
            Console.WriteLine("### Start of script result logs ###");
            foreach (var log in finalStatus.Logs)
            {
                Console.WriteLine(log.Text);
            }

            Console.WriteLine("### End of script result logs ###");
        }
    }
}
