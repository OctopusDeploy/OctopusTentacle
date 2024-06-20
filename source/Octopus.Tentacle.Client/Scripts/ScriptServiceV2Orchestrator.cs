using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.Logging;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Client.Scripts
{
    class ScriptServiceV2Orchestrator : IStructuredScriptOrchestrator
    {
        readonly IAsyncClientScriptServiceV2 clientScriptServiceV2;
        readonly RpcCallExecutor rpcCallExecutor;
        readonly ClientOperationMetricsBuilder clientOperationMetricsBuilder;
        readonly TimeSpan onCancellationAbandonCompleteScriptAfter;
        readonly ITentacleClientTaskLog logger;
        readonly TentacleClientOptions clientOptions;

        public ScriptServiceV2Orchestrator(
            IAsyncClientScriptServiceV2 clientScriptServiceV2,
            RpcCallExecutor rpcCallExecutor,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            TimeSpan onCancellationAbandonCompleteScriptAfter,
            TentacleClientOptions clientOptions,
            ITentacleClientTaskLog logger)
        {
            this.clientScriptServiceV2 = clientScriptServiceV2;
            this.rpcCallExecutor = rpcCallExecutor;
            this.clientOperationMetricsBuilder = clientOperationMetricsBuilder;
            this.onCancellationAbandonCompleteScriptAfter = onCancellationAbandonCompleteScriptAfter;
            this.clientOptions = clientOptions;
            this.logger = logger;
        }

        StartScriptCommandV2 Map(ExecuteScriptCommand command)
        {
            if (command is not ExecuteShellScriptCommand shellScriptCommand)
                throw new InvalidOperationException($"{nameof(ScriptServiceV2Orchestrator)} only supports commands of type {nameof(ExecuteShellScriptCommand)}.");

            return new StartScriptCommandV2(
                shellScriptCommand.ScriptBody,
                shellScriptCommand.IsolationConfiguration.IsolationLevel,
                shellScriptCommand.IsolationConfiguration.MutexTimeout,
                shellScriptCommand.IsolationConfiguration.MutexName,
                shellScriptCommand.Arguments,
                shellScriptCommand.TaskId,
                shellScriptCommand.ScriptTicket,
                shellScriptCommand.DurationToWaitForScriptToFinish,
                shellScriptCommand.Scripts,
                shellScriptCommand.Files.ToArray());
        }
        
        (ScriptStatus, ITicketForNextStatus) Map(ScriptStatusResponseV2 r)
        {
            return (MapToScriptStatus(r), MapToNextStatus(r));
        }
        
        private ScriptStatus MapToScriptStatus(ScriptStatusResponseV2 scriptStatusResponse)
        {
            return new ScriptStatus(scriptStatusResponse.State, scriptStatusResponse.ExitCode, scriptStatusResponse.Logs);
        }

        private ITicketForNextStatus MapToNextStatus(ScriptStatusResponseV2 scriptStatusResponse)
        {
            return new DefaultTicketForNextStatus(scriptStatusResponse.Ticket, scriptStatusResponse.NextLogSequence, ScriptServiceVersion.ScriptServiceVersion1);
        }
        public ProcessState GetState(ScriptStatusResponseV2 response) => response.State;

        public async Task<(ScriptStatus, ITicketForNextStatus)> StartScript(ExecuteScriptCommand executeScriptCommand, CancellationToken scriptExecutionCancellationToken)
        {
            var command = Map(executeScriptCommand);
            ScriptStatusResponseV2 scriptStatusResponse;
            var startScriptCallsConnectedCount = 0;
            try
            {
                async Task<ScriptStatusResponseV2> StartScriptAction(CancellationToken ct)
                {
                    ++startScriptCallsConnectedCount;
                    var result = await clientScriptServiceV2.StartScriptAsync(command, new HalibutProxyRequestOptions(ct));

                    return result;
                }

                void OnErrorAction(Exception ex)
                {
                    // If we can guarantee that the call to StartScript has not connected to the Service then we can decrement the count
                    if (ex.IsConnectionException())
                    {
                        --startScriptCallsConnectedCount;
                    }
                }

                scriptStatusResponse = await rpcCallExecutor.Execute(
                    retriesEnabled: clientOptions.RpcRetrySettings.RetriesEnabled,
                    RpcCall.Create<IScriptServiceV2>(nameof(IScriptServiceV2.StartScript)),
                    StartScriptAction,
                    OnErrorAction,
                    logger,
                    clientOperationMetricsBuilder,
                    scriptExecutionCancellationToken).ConfigureAwait(false);

                return (MapToScriptStatus(scriptStatusResponse), MapToNextStatus(scriptStatusResponse));
            }
            catch (Exception ex) when (scriptExecutionCancellationToken.IsCancellationRequested)
            {
                // If the call to StartScript is in-flight (being transferred to the Service) or we are retrying the StartScript call
                // then we do not know if the script has been started or not on Tentacle so need to call CancelScript and CompleteScript
                var startScriptCallIsBeingRetried = startScriptCallsConnectedCount > 1;

                // We determine if the call was connecting when cancelled, then assume it's transferring if it is not connecting.
                // This is the safest option as it will default to the CancelScript CompleteScript path if we are unsure
                var startScriptCallIsConnecting = ex.IsConnectionException();

                if (!startScriptCallIsConnecting || startScriptCallIsBeingRetried)
                {
                    var scriptStatus = new ScriptStatus(ProcessState.Pending, null, new List<ProcessOutput>());
                    var defaultTicketForNextStatus = new DefaultTicketForNextStatus(command.ScriptTicket, 0, ScriptServiceVersion.ScriptServiceVersion2);
                    return (scriptStatus, defaultTicketForNextStatus);
                }

                // If the StartScript call was not in-flight or being retries then we know the script has not started executing on Tentacle
                // So can exit without calling CancelScript or CompleteScript
                throw new OperationCanceledException("Script execution was cancelled", ex);
            }
        }

        public async Task<(ScriptStatus, ITicketForNextStatus)> GetStatus(ITicketForNextStatus lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            return Map(await _GetStatus(lastStatusResponse, scriptExecutionCancellationToken));

        }
        
        private async Task<ScriptStatusResponseV2> _GetStatus(ITicketForNextStatus lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            
            async Task<ScriptStatusResponseV2> GetStatusAction(CancellationToken ct)
            {
                var request = new ScriptStatusRequestV2(lastStatusResponse.ScriptTicket, lastStatusResponse.NextLogSequence);
                var result = await clientScriptServiceV2.GetStatusAsync(request, new HalibutProxyRequestOptions(ct));

                return result;
            }

            return await rpcCallExecutor.Execute(
                retriesEnabled: clientOptions.RpcRetrySettings.RetriesEnabled,
                RpcCall.Create<IScriptServiceV2>(nameof(IScriptServiceV2.GetStatus)),
                GetStatusAction,
                logger,
                clientOperationMetricsBuilder,
                scriptExecutionCancellationToken).ConfigureAwait(false);
        }

        public async Task<(ScriptStatus, ITicketForNextStatus)> Cancel(ITicketForNextStatus lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            return Map(await _Cancel(lastStatusResponse, scriptExecutionCancellationToken)); 
        }

        private async Task<ScriptStatusResponseV2> _Cancel(ITicketForNextStatus lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            async Task<ScriptStatusResponseV2> CancelScriptAction(CancellationToken ct)
            {
                var request = new CancelScriptCommandV2(lastStatusResponse.ScriptTicket, lastStatusResponse.NextLogSequence);
                var result = await clientScriptServiceV2.CancelScriptAsync(request, new HalibutProxyRequestOptions(ct));

                return result;
            }

            // TODO: SaST - This could be optimized for the failure scenario.
            // If script execution is already triggering RPC Retries and then the script execution is cancelled there is a high chance that the cancel RPC call will fail as well and go into RPC retries.
            // We could potentially reduce the time to failure by not retrying the cancel RPC Call if the previous RPC call was already triggering RPC Retries.

            return await rpcCallExecutor.Execute(
                retriesEnabled: clientOptions.RpcRetrySettings.RetriesEnabled,
                RpcCall.Create<IScriptServiceV2>(nameof(IScriptServiceV2.CancelScript)),
                CancelScriptAction,
                logger,
                clientOperationMetricsBuilder,
                // We don't want to cancel this operation as it is responsible for stopping the script executing on the Tentacle
                CancellationToken.None).ConfigureAwait(false);
        }

        public async Task<ScriptStatus?> Finish(ITicketForNextStatus lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            await _Finish(lastStatusResponse, scriptExecutionCancellationToken);
            return null;
        }

        private async Task _Finish(ITicketForNextStatus lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            try
            {
                // Finish performs a best effort cleanup of the Workspace on Tentacle
                // If we are cancelling script execution we abandon a call to complete script after a period of time

                using var completeScriptCancellationTokenSource = new CancellationTokenSource();

                await using var _ = scriptExecutionCancellationToken.Register(() => completeScriptCancellationTokenSource.CancelAfter(onCancellationAbandonCompleteScriptAfter));

                await rpcCallExecutor.ExecuteWithNoRetries(
                        RpcCall.Create<IScriptServiceV2>(nameof(IScriptServiceV2.CompleteScript)),
                        async ct =>
                        {
                            var request = new CompleteScriptCommandV2(lastStatusResponse.ScriptTicket);
                            await clientScriptServiceV2.CompleteScriptAsync(request, new HalibutProxyRequestOptions(ct));
                        },
                        logger,
                        clientOperationMetricsBuilder,
                        completeScriptCancellationTokenSource.Token);
            }
            catch (Exception ex) when (ex is HalibutClientException or OperationCanceledException)
            {
                logger.Warn("Failed to cleanup the script working directory on Tentacle");
                logger.Verbose(ex);
            }
        }
    }
}