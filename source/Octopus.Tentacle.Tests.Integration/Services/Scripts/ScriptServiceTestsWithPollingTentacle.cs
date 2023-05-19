using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.TentacleClient;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Services.Scripts
{
    public class ScriptServiceTestsWithPollingTentacle
    {
        [Test]
        public async Task RunScriptWithSuccess()
        {
            var psScript = @"
                Write-Host ""This is the start of the script""
                Write-Host ""The answer is"" (6 * 7)
                Start-Sleep -Seconds 3
                Write-Host ""This is the end of the script""";

            var bashScript = @"
                echo This is the start of the script
                val=6
                ((theAnswer=$val*7))
                echo The answer is $theAnswer
                sleep 3
                echo This is the end of the script";

            var finalStatus = await RunScriptOnLocalPollingTentacle(psScript, bashScript);

            finalStatus.State.Should().Be(ProcessState.Complete);
            finalStatus.ExitCode.Should().Be(0);
            finalStatus.Logs.Select(x => x.Text).Should().Contain("The answer is 42");
        }

        [Test]
        public async Task RunScriptWithErrors()
        {
            var psScript = @"
                Write-Host ""This is the start of the script""
                Start-Sleep -Seconds 3
                throw ""Whoopsy Daisy!""
                Write-Host ""This is the end of the script""";

            var bashScript = @"
                echo This is the start of the script
                sleep 3
                echo ""Whoopsy Daisy!""
                exit 1
                echo This is the end of the script""";

            var finalStatus = await RunScriptOnLocalPollingTentacle(psScript, bashScript);

            finalStatus.State.Should().Be(ProcessState.Complete);
            finalStatus.ExitCode.Should().NotBe(0);
            finalStatus.Logs.Select(x => x.Text).Should().Contain("Whoopsy Daisy!");
            finalStatus.Logs.Select(x => x.Text).Should().NotContain("This is the end of the script");
        }

        async Task<ScriptStatusResponse> RunScriptOnLocalPollingTentacle(string psScript, string bashScript)
        {
            using IHalibutRuntime octopus = new HalibutRuntimeBuilder()
                .WithServerCertificate(Support.Certificates.Server)
                .WithMessageSerializer(s => s.WithLegacyContractSupport())
                .Build();

            var port = octopus.Listen();
            octopus.Trust(Support.Certificates.TentaclePublicThumbprint);

            using (var runningTentacle = await new PollingTentacleBuilder(port, Support.Certificates.ServerPublicThumbprint)
                       .Build(CancellationToken.None))
            {
                var tentacleClient = new TentacleClientBuilder(octopus)
                    .ForRunningTentacle(runningTentacle)
                    .Build(CancellationToken.None);

                var startScriptCommand = new StartScriptCommandBuilder()
                    .WithScriptBody(PlatformDetection.IsRunningOnWindows ? psScript : bashScript)
                    .Build();

                var scriptTicket = tentacleClient.ScriptService.StartScript(startScriptCommand);
                while (tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(scriptTicket, 0)).State != ProcessState.Complete)
                {
                    Thread.Sleep(100);
                }

                var finalStatus = tentacleClient.ScriptService.CompleteScript(new CompleteScriptCommand(scriptTicket, 0));

                Console.WriteLine("### Start of script result logs ###");
                foreach (var log in finalStatus.Logs)
                {
                    Console.WriteLine(log.Text);
                }

                Console.WriteLine("### End of script result logs ###");

                return finalStatus;
            }
        }
    }
}
