using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Util;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Support.Legacy
{
    class ScriptExecutionOrchestrator
    {
        readonly LegacyTentacleClient tentacleClient;
        readonly ILogger logger;

        public ScriptExecutionOrchestrator(LegacyTentacleClient tentacleClient, ILogger logger)
        {
            this.tentacleClient = tentacleClient;
            this.logger = logger;
        }

        public async Task<ScriptStatusResponse> ExecuteScript(string windowsScript, string nixScript, CancellationToken cancellationToken)
        {
            var scriptTicket = await StartScript(windowsScript, nixScript, cancellationToken);
            
            var scriptStatusResponse = await ObserverUntilComplete(scriptTicket, cancellationToken);

            scriptStatusResponse = await CompleteScript(scriptStatusResponse, cancellationToken);

            return scriptStatusResponse;
        }

        public async Task<ScriptTicket> StartScript(string windowsScript, string nixScript, CancellationToken cancellationToken)
        {
            logger.Information("Starting script execution");

            var startScriptCommand = new StartScriptCommandBuilder()
                .WithScriptBody(PlatformDetection.IsRunningOnWindows ? windowsScript : nixScript)
                .Build();

            cancellationToken.ThrowIfCancellationRequested();

            var scriptTicket = await tentacleClient.ScriptService.StartScriptAsync(startScriptCommand, new(cancellationToken, null));
            
            logger.Information("Started script execution");

            return scriptTicket;
        }

        public async Task<ScriptStatusResponse> ObserverUntilScriptOutputReceived(ScriptTicket scriptTicket, string outputContains, CancellationToken cancellationToken)
        {
            var scriptStatusResponse = new ScriptStatusResponse(scriptTicket, ProcessState.Pending, 0, new List<ProcessOutput>(), 0);
            var logs = new List<ProcessOutput>();

            while (scriptStatusResponse.State != ProcessState.Complete)
            {
                cancellationToken.ThrowIfCancellationRequested();

                logger.Information("Starting GetStatus call");

                scriptStatusResponse = await tentacleClient.ScriptService.GetStatusAsync(
                    new ScriptStatusRequest(scriptTicket, scriptStatusResponse.NextLogSequence),
                    new(cancellationToken, null));
                
                logs.AddRange(scriptStatusResponse.Logs);

                logger.Information("Current script status: {ScriptStatus}", scriptStatusResponse.State);

                foreach (var log in scriptStatusResponse.Logs)
                {
                    logger.Information("Script Logs: {Logs}", log.Text);
                }

                if (scriptStatusResponse.Logs.Any(l => l.Text.Contains(outputContains)))
                {
                    break;
                }

                if (scriptStatusResponse.State != ProcessState.Complete)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
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

                logger.Information("Starting GetStatus call");

                scriptStatusResponse = await tentacleClient.ScriptService.GetStatusAsync(
                    new ScriptStatusRequest(scriptTicket, scriptStatusResponse.NextLogSequence),
                    new(cancellationToken, null));
                
                logger.Information("Current script status: {ScriptStatus}", scriptStatusResponse.State);

                foreach (var log in scriptStatusResponse.Logs)
                {
                    logger.Information("Script Logs: {Logs}", log.Text);
                }

                logs.AddRange(scriptStatusResponse.Logs);

                if (scriptStatusResponse.State != ProcessState.Complete)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
            }

            return new ScriptStatusResponse(scriptStatusResponse.Ticket, scriptStatusResponse.State, scriptStatusResponse.ExitCode, logs, scriptStatusResponse.NextLogSequence);
        }

        public async Task<ScriptStatusResponse> CompleteScript(ScriptStatusResponse scriptStatusResponse, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            logger.Information("Starting CompleteScript call");

            var finalStatus = await tentacleClient.ScriptService.CompleteScriptAsync(
                new CompleteScriptCommand(scriptStatusResponse.Ticket, scriptStatusResponse.NextLogSequence),
                new(cancellationToken, null));
            
            var logs = new List<ProcessOutput>();
            logs.AddRange(scriptStatusResponse.Logs);
            logs.AddRange(finalStatus.Logs);

            logger.Information("Current script status: {ScriptStatus}", finalStatus.State);

            foreach (var log in finalStatus.Logs)
            {
                logger.Information("Script Logs: {Logs}", log.Text);
            }

            return new ScriptStatusResponse(finalStatus.Ticket, finalStatus.State, finalStatus.ExitCode, logs, finalStatus.NextLogSequence);
        }
    }
}
