using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.CommonTestUtils.Builders;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Tests.Integration.Util;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Integration.Support.Legacy
{
    internal class ScriptExecutionOrchestrator
    {
        private readonly LegacyTentacleClient tentacleClient;
        private readonly SyncOrAsyncHalibut syncOrAsyncHalibut;

        public ScriptExecutionOrchestrator(LegacyTentacleClient tentacleClient, SyncOrAsyncHalibut syncOrAsyncHalibut)
        {
            this.tentacleClient = tentacleClient;
            this.syncOrAsyncHalibut = syncOrAsyncHalibut;
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
            var startScriptCommand = new StartScriptCommandBuilder()
                .WithScriptBody(PlatformDetection.IsRunningOnWindows ? windowsScript : nixScript)
                .Build();

            cancellationToken.ThrowIfCancellationRequested();

            var scriptTicket = await syncOrAsyncHalibut
                .WhenSync(() => tentacleClient.ScriptService.SyncService.StartScript(startScriptCommand))
                .WhenAsync(async () => await tentacleClient.ScriptService.AsyncService.StartScriptAsync(startScriptCommand, new(cancellationToken, null)));
            
            return scriptTicket;
        }

        public async Task<ScriptStatusResponse> ObserverUntilScriptOutputReceived(ScriptTicket scriptTicket, string output, CancellationToken cancellationToken)
        {
            var scriptStatusResponse = new ScriptStatusResponse(scriptTicket, ProcessState.Pending, 0, new List<ProcessOutput>(), 0);
            var logs = new List<ProcessOutput>();
            var logger = new SerilogLoggerBuilder().Build();
            logger.Information("ObserverUntilScriptOutputReceived started");

            while (scriptStatusResponse.State != ProcessState.Complete)
            {
                cancellationToken.ThrowIfCancellationRequested();

                logger.Information("About to call GetStatus");
                scriptStatusResponse = await syncOrAsyncHalibut
                    .WhenSync(() =>
                    {
                        logger.Information("Calling GetStatus(sync)");
                        var result = tentacleClient.ScriptService.SyncService.GetStatus(new ScriptStatusRequest(scriptTicket, scriptStatusResponse.NextLogSequence));
                        logger.Information("GetStatus(sync) complete");
                        return result;
                    })
                    .WhenAsync(async () =>
                    {
                        logger.Information("Calling GetStatus(sync)");
                        var result = await tentacleClient.ScriptService.AsyncService.GetStatusAsync(
                            new ScriptStatusRequest(scriptTicket, scriptStatusResponse.NextLogSequence),
                            new(cancellationToken, null));
                        logger.Information("GetStatus(sync) complete");
                        return result;
                    });
                logger.Information($"GetStatus complete, status response state: {scriptStatusResponse.State}");
                
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

            logger.Information("ObserverUntilScriptOutputReceived complete");
            return new ScriptStatusResponse(scriptStatusResponse.Ticket, scriptStatusResponse.State, scriptStatusResponse.ExitCode, logs, scriptStatusResponse.NextLogSequence);
        }

        public async Task<ScriptStatusResponse> ObserverUntilComplete(ScriptTicket scriptTicket, CancellationToken cancellationToken)
        {
            var scriptStatusResponse = new ScriptStatusResponse(scriptTicket, ProcessState.Pending, 0, new List<ProcessOutput>(), 0);
            var logs = new List<ProcessOutput>();

            while (scriptStatusResponse.State != ProcessState.Complete)
            {
                cancellationToken.ThrowIfCancellationRequested();

                scriptStatusResponse = await syncOrAsyncHalibut
                    .WhenSync(() => tentacleClient.ScriptService.SyncService.GetStatus(new ScriptStatusRequest(scriptTicket, scriptStatusResponse.NextLogSequence)))
                    .WhenAsync(async () => await tentacleClient.ScriptService.AsyncService.GetStatusAsync(
                        new ScriptStatusRequest(scriptTicket, scriptStatusResponse.NextLogSequence),
                        new(cancellationToken, null)));
                
                logs.AddRange(scriptStatusResponse.Logs);

                if (scriptStatusResponse.State != ProcessState.Complete)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                }
            }

            return new ScriptStatusResponse(scriptStatusResponse.Ticket, scriptStatusResponse.State, scriptStatusResponse.ExitCode, logs, scriptStatusResponse.NextLogSequence);
        }

        public async Task<ScriptStatusResponse> CompleteScript(ScriptStatusResponse scriptStatusResponse, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var finalStatus = await syncOrAsyncHalibut
                .WhenSync(() => tentacleClient.ScriptService.SyncService.CompleteScript(new CompleteScriptCommand(scriptStatusResponse.Ticket, scriptStatusResponse.NextLogSequence)))
                .WhenAsync(async () => await tentacleClient.ScriptService.AsyncService.CompleteScriptAsync(
                    new CompleteScriptCommand(scriptStatusResponse.Ticket, scriptStatusResponse.NextLogSequence),
                    new(cancellationToken, null)));
            
            var logs = new List<ProcessOutput>();
            logs.AddRange(scriptStatusResponse.Logs);
            logs.AddRange(finalStatus.Logs);

            return new ScriptStatusResponse(finalStatus.Ticket, finalStatus.State, finalStatus.ExitCode, logs, finalStatus.NextLogSequence);
        }
    }
}
