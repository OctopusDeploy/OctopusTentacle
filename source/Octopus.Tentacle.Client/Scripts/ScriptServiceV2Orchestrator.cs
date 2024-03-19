using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.Exceptions;
using Halibut.ServiceModel;
using Halibut.Transport;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using ILog = Octopus.Diagnostics.ILog;

namespace Octopus.Tentacle.Client.Scripts
{
    class ScriptServiceV2Orchestrator : ObservingScriptOrchestrator<StartScriptCommandV2, ScriptStatusResponseV2>
    {
        readonly IAsyncClientScriptServiceV2 clientScriptServiceV2;
        readonly RpcCallExecutor rpcCallExecutor;
        readonly ClientOperationMetricsBuilder clientOperationMetricsBuilder;
        readonly TimeSpan onCancellationAbandonCompleteScriptAfter;
        readonly ILog logger;

        public ScriptServiceV2Orchestrator(
            IAsyncClientScriptServiceV2 clientScriptServiceV2,
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            RpcCallExecutor rpcCallExecutor,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            TimeSpan onCancellationAbandonCompleteScriptAfter,
            TentacleClientOptions clientOptions,
            ILog logger)
            : base(scriptObserverBackOffStrategy,
                onScriptStatusResponseReceived,
                onScriptCompleted,
                clientOptions)
        {
            this.clientScriptServiceV2 = clientScriptServiceV2;
            this.rpcCallExecutor = rpcCallExecutor;
            this.clientOperationMetricsBuilder = clientOperationMetricsBuilder;
            this.onCancellationAbandonCompleteScriptAfter = onCancellationAbandonCompleteScriptAfter;
            this.logger = logger;
        }

        protected override StartScriptCommandV2 Map(ExecuteScriptCommand command)
            => new(
                command.ScriptBody,
                command.Isolation,
                command.ScriptIsolationMutexTimeout,
                command.IsolationMutexName!,
                command.Arguments,
                command.TaskId,
                command.ScriptTicket,
                command.DurationToWaitForScriptToFinish,
                command.Scripts,
                command.Files.ToArray());

        protected override ScriptExecutionStatus MapToStatus(ScriptStatusResponseV2 response)
            => new(response.Logs);

        protected override ScriptExecutionResult MapToResult(ScriptStatusResponseV2 response)
            => new(response.State, response.ExitCode);

        protected override ProcessState GetState(ScriptStatusResponseV2 response) => response.State;

        protected override async Task<ScriptStatusResponseV2> StartScript(StartScriptCommandV2 command, CancellationToken scriptExecutionCancellationToken)
        {
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
                    retriesEnabled: ClientOptions.RpcRetrySettings.RetriesEnabled,
                    RpcCall.Create<IScriptServiceV2>(nameof(IScriptServiceV2.StartScript)),
                    StartScriptAction,
                    OnErrorAction,
                    logger,
                    clientOperationMetricsBuilder,
                    scriptExecutionCancellationToken).ConfigureAwait(false);
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
                    // We have to assume the script started executing and call CancelScript and CompleteScript
                    // We don't have a response so we need to create one to continue the execution flow
                    scriptStatusResponse = new ScriptStatusResponseV2(
                        command.ScriptTicket,
                        ProcessState.Pending,
                        ScriptExitCodes.RunningExitCode,
                        new List<ProcessOutput>(),
                        0);

                    try
                    {
                        await ObserveUntilCompleteThenFinish(scriptStatusResponse, scriptExecutionCancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception observerUntilCompleteException)
                    {
                        // Throw an error so the caller knows that execution of the script was cancelled
                        throw new OperationCanceledException("Script execution was cancelled", observerUntilCompleteException);
                    }

                    // Throw an error so the caller knows that execution of the script was cancelled
                    throw new OperationCanceledException("Script execution was cancelled");
                }

                // If the StartScript call was not in-flight or being retries then we know the script has not started executing on Tentacle
                // So can exit without calling CancelScript or CompleteScript
                throw new OperationCanceledException("Script execution was cancelled", ex);
            }

            return scriptStatusResponse;
        }

        protected override async Task<ScriptStatusResponseV2> GetStatus(ScriptStatusResponseV2 lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            try
            {
                async Task<ScriptStatusResponseV2> GetStatusAction(CancellationToken ct)
                {
                    var request = new ScriptStatusRequestV2(lastStatusResponse.Ticket, lastStatusResponse.NextLogSequence);
                    var result = await clientScriptServiceV2.GetStatusAsync(request, new HalibutProxyRequestOptions(ct));

                    return result;
                }

                return await rpcCallExecutor.Execute(
                    retriesEnabled: ClientOptions.RpcRetrySettings.RetriesEnabled,
                    RpcCall.Create<IScriptServiceV2>(nameof(IScriptServiceV2.GetStatus)),
                    GetStatusAction,
                    logger,
                    clientOperationMetricsBuilder,
                    scriptExecutionCancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException && scriptExecutionCancellationToken.IsCancellationRequested)
            {
                // Return the last known response without logs when cancellation occurs and let the script execution go into the CancelScript and CompleteScript flow
                return new ScriptStatusResponseV2(lastStatusResponse.Ticket, lastStatusResponse.State, lastStatusResponse.ExitCode, new List<ProcessOutput>(), lastStatusResponse.NextLogSequence);
            }
        }

        protected override async Task<ScriptStatusResponseV2> Cancel(ScriptStatusResponseV2 lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            async Task<ScriptStatusResponseV2> CancelScriptAction(CancellationToken ct)
            {
                var request = new CancelScriptCommandV2(lastStatusResponse.Ticket, lastStatusResponse.NextLogSequence);
                var result = await clientScriptServiceV2.CancelScriptAsync(request, new HalibutProxyRequestOptions(ct));

                return result;
            }

            // TODO: SaST - This could be optimized for the failure scenario.
            // If script execution is already triggering RPC Retries and then the script execution is cancelled there is a high chance that the cancel RPC call will fail as well and go into RPC retries.
            // We could potentially reduce the time to failure by not retrying the cancel RPC Call if the previous RPC call was already triggering RPC Retries.

            return await rpcCallExecutor.Execute(
                retriesEnabled: ClientOptions.RpcRetrySettings.RetriesEnabled,
                RpcCall.Create<IScriptServiceV2>(nameof(IScriptServiceV2.CancelScript)),
                CancelScriptAction,
                logger,
                clientOperationMetricsBuilder,
                // We don't want to cancel this operation as it is responsible for stopping the script executing on the Tentacle
                CancellationToken.None).ConfigureAwait(false);
        }

        protected override async Task<ScriptStatusResponseV2> Finish(ScriptStatusResponseV2 lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
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
                            var request = new CompleteScriptCommandV2(lastStatusResponse.Ticket);
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

            return lastStatusResponse;
        }
    }
}