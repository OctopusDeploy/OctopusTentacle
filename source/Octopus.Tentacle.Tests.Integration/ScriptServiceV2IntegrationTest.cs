using System;
using System.Collections.Generic;
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

            using (var runningTentacle = new PollingTentacleBuilder(port, Support.Certificates.ServerPublicThumbprint)
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

                var allLogs = String.Join(" ", logs.Select(l => l.Text).ToArray());

                allLogs.Should().Contain("hello");
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