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
        public async Task SuccessfullyRunScript()
        {
            var psScript = @"
                                Write-Host ""This is the start of the script""
                                Write-Host ""The answer is"" (6 * 7)
                                Write-Host ""Going to sleep for 3 seconds, current time is"" (Get-Date -DisplayHint Time)
                                Start-Sleep -Seconds 3
                                Write-Host ""Waking up, current time is"" (Get-Date -DisplayHint Time)
                                Write-Host ""This is the end of the script""";

            // var cmdScript = @"
            //                     echo This is the start of the script
            //                     set /A theAnswer = 6 * 7
            //                     echo The answer is %theAnswer%
            //                     ping localhost -n 4 > nul
            //                     echo This is the end of the script";

            var bashScript = @"
                                echo This is the start of the script
                                val=6
                                ((theAnswer=$val*7))
                                echo The answer is $theAnswer
                                sleep 3
                                echo This is the end of the script";
            
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

                finalStatus.State.Should().Be(ProcessState.Complete);
                finalStatus.ExitCode.Should().Be(0);
                finalStatus.Logs.Select(x => x.Text).Should().Contain("The answer is 42");
            }
        }
    }
}
