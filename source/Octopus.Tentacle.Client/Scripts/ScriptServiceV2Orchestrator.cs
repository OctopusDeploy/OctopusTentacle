using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
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

        protected override StartScriptCommandV2 Map(StartScriptCommandV2 command) => command;

        protected override ScriptExecutionStatus MapToStatus(ScriptStatusResponseV2 response)
            => new(response.Logs);

        protected override ScriptExecutionResult MapToResult(ScriptStatusResponseV2 response)
            => new(response.State, response.ExitCode);

        protected override ProcessState GetState(ScriptStatusResponseV2 response) => response.State;

        protected override async Task<ScriptStatusResponseV2> StartScript(StartScriptCommandV2 command, CancellationToken scriptExecutionCancellationToken)
        {
            ScriptStatusResponseV2 scriptStatusResponse;
            var startScriptCallCount = 0;
            try
            {
                async Task<ScriptStatusResponseV2> StartScriptAction(CancellationToken ct)
                {
                    ++startScriptCallCount;

                    var result = await clientScriptServiceV2.StartScriptAsync(command, new HalibutProxyRequestOptions(ct, CancellationToken.None));

                    return result;
                }

                if (ClientOptions.RpcRetrySettings.RetriesEnabled)
                {
                    scriptStatusResponse = await rpcCallExecutor.ExecuteWithRetries(
                        RpcCall.Create<IScriptServiceV2>(nameof(IScriptServiceV2.StartScript)),
                        StartScriptAction,
                        logger,
                        // If we are cancelling script execution we can abandon a call to start script
                        // If we manage to cancel the start script call we can walk away
                        // If we do abandon the start script call we have to assume the script is running so need
                        // to call CancelScript and CompleteScript
                        abandonActionOnCancellation: true,
                        clientOperationMetricsBuilder,
                        scriptExecutionCancellationToken).ConfigureAwait(false);
                }
                else
                {
                    scriptStatusResponse = await rpcCallExecutor.ExecuteWithNoRetries(
                        RpcCall.Create<IScriptServiceV2>(nameof(IScriptServiceV2.StartScript)),
                        StartScriptAction,
                        logger,
                        abandonActionOnCancellation: true,
                        clientOperationMetricsBuilder,
                        scriptExecutionCancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception e) when (e is OperationCanceledException && scriptExecutionCancellationToken.IsCancellationRequested)
            {
                // If we are not retrying and we managed to cancel execution it means the request was never sent so we can safely walk away from it.
                if (e is not OperationAbandonedException && startScriptCallCount <= 1)
                {
                    throw;
                }

                // Otherwise we have to assume the script started executing and call CancelScript and CompleteScript
                // We don't have a response so we need to create one to continue the execution flow
                scriptStatusResponse = new ScriptStatusResponseV2(
                    command.ScriptTicket,
                    ProcessState.Pending,
                    ScriptExitCodes.RunningExitCode,
                    new List<ProcessOutput>(),
                    0);

                await ObserveUntilCompleteThenFinish(scriptStatusResponse, scriptExecutionCancellationToken).ConfigureAwait(false);

                // Throw an error so the caller knows that execution of the script was cancelled
                throw new OperationCanceledException("Script execution was cancelled");
            }

            return scriptStatusResponse;
        }

        protected override async Task<ScriptStatusResponseV2> GetStatus(ScriptStatusResponseV2 lastStatusResponse, CancellationToken cancellationToken)
        {
            try
            {
                async Task<ScriptStatusResponseV2> GetStatusAction(CancellationToken ct)
                {
                    var request = new ScriptStatusRequestV2(lastStatusResponse.Ticket, lastStatusResponse.NextLogSequence);

                    var result = await clientScriptServiceV2.GetStatusAsync(request, new HalibutProxyRequestOptions(ct, CancellationToken.None));

                    return result;
                }

                if (ClientOptions.RpcRetrySettings.RetriesEnabled)
                {
                    return await rpcCallExecutor.ExecuteWithRetries(
                        RpcCall.Create<IScriptServiceV2>(nameof(IScriptServiceV2.GetStatus)),
                        GetStatusAction,
                        logger,
                        // If cancelling script execution we can abandon a call to GetStatus and go straight into the CancelScript and CompleteScript flow
                        abandonActionOnCancellation: true,
                        clientOperationMetricsBuilder,
                        cancellationToken).ConfigureAwait(false);
                }

                return await rpcCallExecutor.ExecuteWithNoRetries(
                    RpcCall.Create<IScriptServiceV2>(nameof(IScriptServiceV2.GetStatus)),
                    GetStatusAction,
                    logger,
                    abandonActionOnCancellation: true,
                    clientOperationMetricsBuilder,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                // Return the last known response without logs when cancellation occurs and let the script execution go into the CancelScript and CompleteScript flow
                return new ScriptStatusResponseV2(lastStatusResponse.Ticket, lastStatusResponse.State, lastStatusResponse.ExitCode, new List<ProcessOutput>(), lastStatusResponse.NextLogSequence);
            }
        }

        protected override async Task<ScriptStatusResponseV2> Cancel(ScriptStatusResponseV2 lastStatusResponse, CancellationToken cancellationToken)
        {
            async Task<ScriptStatusResponseV2> CancelScriptAction(CancellationToken ct)
            {
                var request = new CancelScriptCommandV2(lastStatusResponse.Ticket, lastStatusResponse.NextLogSequence);

                var result = await clientScriptServiceV2.CancelScriptAsync(request, new HalibutProxyRequestOptions(ct, CancellationToken.None));

                return result;
            }

            if (ClientOptions.RpcRetrySettings.RetriesEnabled)
            {
                return await rpcCallExecutor.ExecuteWithRetries(
                    RpcCall.Create<IScriptServiceV2>(nameof(IScriptServiceV2.CancelScript)),
                    CancelScriptAction,
                    logger,
                    // We don't want to abandon this operation as it is responsible for stopping the script executing on the Tentacle
                    abandonActionOnCancellation: false,
                    clientOperationMetricsBuilder,
                    cancellationToken).ConfigureAwait(false);
            }

            return await rpcCallExecutor.ExecuteWithNoRetries(
                RpcCall.Create<IScriptServiceV2>(nameof(IScriptServiceV2.CancelScript)),
                CancelScriptAction,
                logger,
                abandonActionOnCancellation: false,
                clientOperationMetricsBuilder,
                cancellationToken).ConfigureAwait(false);
        }

        protected override async Task<ScriptStatusResponseV2> Finish(ScriptStatusResponseV2 lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            // Best effort cleanup of Tentacle
            try
            {
                var actionTask =
                    rpcCallExecutor.ExecuteWithNoRetries(
                        RpcCall.Create<IScriptServiceV2>(nameof(IScriptServiceV2.CompleteScript)),
                        async ct =>
                        {
                            var request = new CompleteScriptCommandV2(lastStatusResponse.Ticket);

                            await clientScriptServiceV2.CompleteScriptAsync(request, new HalibutProxyRequestOptions(ct, CancellationToken.None));
                        },
                        logger,
                        abandonActionOnCancellation: false,
                        clientOperationMetricsBuilder,
                        CancellationToken.None);

                var actionTaskCompletionResult = await actionTask.WaitTillCompletion(onCancellationAbandonCompleteScriptAfter, scriptExecutionCancellationToken);
                if (actionTaskCompletionResult == TaskCompletionResult.Abandoned)
                {
                    throw new OperationAbandonedException(onCancellationAbandonCompleteScriptAfter);
                }

                await actionTask;
            }
            catch (Exception ex) when (ex is HalibutClientException or OperationCanceledException or OperationAbandonedException)
            {
                logger.Warn("Failed to cleanup the script working directory on Tentacle");
                logger.Verbose(ex);
            }

            return lastStatusResponse;
        }
    }
}