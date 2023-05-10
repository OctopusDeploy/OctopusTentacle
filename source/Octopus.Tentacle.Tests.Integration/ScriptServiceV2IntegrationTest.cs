using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut;
using NUnit.Framework;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Legacy;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Tests.Integration.TentacleClient;

namespace Octopus.Tentacle.Tests.Integration
{
    public class ScriptServiceV2IntegrationTest
    {
        [Test]
        public async Task CanRunScript()
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
                
                var bashScript = "echo hello\nsleep 10";
                var windowsScript = "echo hello\r\nStart-Sleep -Seconds 10";

                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                    .Build();

                var response = tentacleClient.ScriptServiceV2.StartScript(startScriptCommand);

                var (logs, finalResponse) = await RunUntilScriptCompletes(startScriptCommand, response, tentacleClient.ScriptServiceV2);

                finalResponse.State.Should().Be(ProcessState.Complete);
                finalResponse.ExitCode.Should().Be(0);

                var allLogs = JoinLogs(logs);

                allLogs.Should().Contain("hello");
            }
        }

        private static string JoinLogs(List<ProcessOutput> logs)
        {
            return String.Join(" ", logs.Select(l => l.Text).ToArray());
        }

        [Test]
        public async Task WhenTentacleRestartsWhileRunningAScript_TheExitCodeShouldBe_UnknownResultExitCode()
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
                
                var bashScript = "echo hello\nsleep 1\necho readytodie\nsleep 100";
                var windowsScript = "echo hello\r\nStart-Sleep -Seconds 1\r\ncho readytodie\r\nStart-Sleep -Seconds 100";

                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                    .Build();

                
                var response = tentacleClient.ScriptServiceV2.StartScript(startScriptCommand);
                while (!JoinLogs(response.Logs).Contains("readytodie"))
                {
                    response = tentacleClient.ScriptServiceV2.GetStatus(new ScriptStatusRequestV2(startScriptCommand.ScriptTicket, 0));
                }

                await runningTentacle.Stop(CancellationToken.None);
                await runningTentacle.Start(CancellationToken.None);
                

                var finalResponse = tentacleClient.ScriptServiceV2.GetStatus(new ScriptStatusRequestV2(startScriptCommand.ScriptTicket, 0));
                
                finalResponse.State.Should().Be(ProcessState.Complete); // This is technically a lie, the process is still running.
                finalResponse.ExitCode.Should().Be(ScriptExitCodes.UnknownResultExitCode);
                JoinLogs(finalResponse.Logs).Should().Contain("readytodie");
            }
        }
        
        
        [Test]
        public async Task WhenALongRunningScriptIsCancelled_TheScriptShouldStop()
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
                
                var bashScript = "echo hello\nsleep 1\necho readytodie\nsleep 100";
                var windowsScript = "echo hello\r\nStart-Sleep -Seconds 1\r\ncho readytodie\r\nStart-Sleep -Seconds 100";

                var startScriptCommand = new StartScriptCommandV2Builder()
                    .WithScriptBodyForCurrentOs(windowsScript, bashScript)
                    .Build();
                
                var response = tentacleClient.ScriptServiceV2.StartScript(startScriptCommand);
                while (!JoinLogs(response.Logs).Contains("readytodie"))
                {
                    response = tentacleClient.ScriptServiceV2.GetStatus(new ScriptStatusRequestV2(startScriptCommand.ScriptTicket, 0));
                }

                tentacleClient.ScriptServiceV2.CancelScript(new CancelScriptCommandV2(startScriptCommand.ScriptTicket, 0));

                var stopWatch = Stopwatch.StartNew();
                var (logs, finalResponse) = await RunUntilScriptCompletes(startScriptCommand, response, tentacleClient.ScriptServiceV2);
                stopWatch.Stop();

                finalResponse.State.Should().Be(ProcessState.Complete); // This is technically a lie, the process is still running.
                finalResponse.ExitCode.Should().NotBe(0);
                stopWatch.Elapsed.Should().BeLessOrEqualTo(TimeSpan.FromSeconds(5));
            }
        }
        
        async Task<(List<ProcessOutput>, ScriptStatusResponseV2)> RunUntilScriptCompletes(StartScriptCommandV2 startScriptCommand, ScriptStatusResponseV2 response, IScriptServiceV2 service)
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

            service.CompleteScript(new CompleteScriptCommandV2(startScriptCommand.ScriptTicket));

            WriteLogsToConsole(logs);

            return (logs, response);
        }

        void WriteLogsToConsole(List<ProcessOutput> logs)
        {
            foreach (var log in logs)
            {
                Console.WriteLine(log.Text);
            }
        }
    }
}