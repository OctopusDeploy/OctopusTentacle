using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support.Legacy
{
    public class ScriptExecutionOrchestrator
    {
        private readonly TentacleClient tentacleClient;

        public ScriptExecutionOrchestrator(TentacleClient tentacleClient)
        {
            this.tentacleClient = tentacleClient;
        }

        public async Task<ScriptStatusResponse> ExecuteScript(string windowsScript, string nixScript, CancellationToken cancellationToken)
        {
            var scriptTicket = StartScript(windowsScript, nixScript, cancellationToken);

            var scriptStatusResponse = await ObserverUntilComplete(scriptTicket, cancellationToken);

            scriptStatusResponse = CompleteScript(scriptStatusResponse, cancellationToken);

            return scriptStatusResponse;
        }

        public ScriptTicket StartScript(string windowsScript, string nixScript, CancellationToken cancellationToken)
        {
            var startScriptCommand = new StartScriptCommandBuilder()
                .WithScriptBody(PlatformDetection.IsRunningOnWindows ? windowsScript : nixScript)
                .Build();

            cancellationToken.ThrowIfCancellationRequested();

            var scriptTicket = tentacleClient.ScriptService.StartScript(startScriptCommand);

            return scriptTicket;
        }

        public async Task<ScriptStatusResponse> ObserverUntilScriptOutputReceived(ScriptTicket scriptTicket, string output, CancellationToken cancellationToken)
        {
            var scriptStatusResponse = new ScriptStatusResponse(scriptTicket, ProcessState.Pending, 0, new List<ProcessOutput>(), 0);
            var logs = new List<ProcessOutput>();

            while (scriptStatusResponse.State != ProcessState.Complete)
            {
                cancellationToken.ThrowIfCancellationRequested();

                scriptStatusResponse = tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(scriptTicket, scriptStatusResponse.NextLogSequence));
                logs.AddRange(scriptStatusResponse.Logs);

                if (logs.Any(l => l.Text == output))
                {
                    break;
                }

                if (scriptStatusResponse.State != ProcessState.Complete)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                }
            }

            return new ScriptStatusResponse(scriptStatusResponse.Ticket, scriptStatusResponse.State, scriptStatusResponse.ExitCode, logs, scriptStatusResponse.NextLogSequence);
        }

        public async Task<ScriptStatusResponse> ObserverUntilComplete(ScriptTicket scriptTicket, CancellationToken cancellationToken)
        {
            var scriptStatusResponse = new ScriptStatusResponse(scriptTicket, ProcessState.Pending, 0, new List<ProcessOutput>(), 0);
            var logs = new List<ProcessOutput>();

            while (scriptStatusResponse.State != ProcessState.Complete)
            {
                cancellationToken.ThrowIfCancellationRequested();

                scriptStatusResponse = tentacleClient.ScriptService.GetStatus(new ScriptStatusRequest(scriptTicket, scriptStatusResponse.NextLogSequence));
                logs.AddRange(scriptStatusResponse.Logs);

                if (scriptStatusResponse.State != ProcessState.Complete)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                }
            }

            return new ScriptStatusResponse(scriptStatusResponse.Ticket, scriptStatusResponse.State, scriptStatusResponse.ExitCode, logs, scriptStatusResponse.NextLogSequence);
        }

        public ScriptStatusResponse CompleteScript(ScriptStatusResponse scriptStatusResponse, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var finalStatus = tentacleClient.ScriptService.CompleteScript(new CompleteScriptCommand(scriptStatusResponse.Ticket, scriptStatusResponse.NextLogSequence));

            var logs = new List<ProcessOutput>();
            logs.AddRange(scriptStatusResponse.Logs);
            logs.AddRange(finalStatus.Logs);

            return new ScriptStatusResponse(finalStatus.Ticket, finalStatus.State, finalStatus.ExitCode, logs, finalStatus.NextLogSequence);
        }
    }
}
