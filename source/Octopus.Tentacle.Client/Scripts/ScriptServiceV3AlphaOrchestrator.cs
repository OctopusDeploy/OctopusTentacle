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
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using ILog = Octopus.Diagnostics.ILog;

namespace Octopus.Tentacle.Client.Scripts
{
    class ScriptServiceV3AlphaOrchestrator : ObservingScriptOrchestrator<StartScriptCommandV3Alpha, ScriptStatusResponseV3Alpha>
    {
        readonly IAsyncClientScriptServiceV3Alpha clientScriptServiceV3Alpha;
        readonly RpcCallExecutor rpcCallExecutor;
        readonly ClientOperationMetricsBuilder clientOperationMetricsBuilder;
        readonly TimeSpan onCancellationAbandonCompleteScriptAfter;
        readonly ILog logger;

        public ScriptServiceV3AlphaOrchestrator(
            IAsyncClientScriptServiceV3Alpha clientScriptServiceV3Alpha,
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
            this.clientScriptServiceV3Alpha = clientScriptServiceV3Alpha;
            this.rpcCallExecutor = rpcCallExecutor;
            this.clientOperationMetricsBuilder = clientOperationMetricsBuilder;
            this.onCancellationAbandonCompleteScriptAfter = onCancellationAbandonCompleteScriptAfter;
            this.logger = logger;
        }

        protected override StartScriptCommandV3Alpha Map(StartScriptCommandV3Alpha command) => command;

        protected override ScriptExecutionStatus MapToStatus(ScriptStatusResponseV3Alpha response)
            => new(response.Logs);

        protected override ScriptExecutionResult MapToResult(ScriptStatusResponseV3Alpha response)
            => new(response.State, response.ExitCode);

        protected override ProcessState GetState(ScriptStatusResponseV3Alpha response) => response.State;

        protected override async Task<ScriptStatusResponseV3Alpha> StartScript(StartScriptCommandV3Alpha command, CancellationToken scriptExecutionCancellationToken)
        {
            ScriptStatusResponseV3Alpha scriptStatusResponse;
            var startScriptCallCount = 0;
            try
            {
                async Task<ScriptStatusResponseV3Alpha> StartScriptAction(CancellationToken ct)
                {
                    ++startScriptCallCount;

                    return await clientScriptServiceV3Alpha.StartScriptAsync(command, new HalibutProxyRequestOptions(ct, CancellationToken.None));
                }

                if (ClientOptions.RpcRetrySettings.RetriesEnabled)
                {
                    scriptStatusResponse = await rpcCallExecutor.ExecuteWithRetries(
                        RpcCall.Create<IScriptServiceV3Alpha>(nameof(IScriptServiceV3Alpha.StartScript)),
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
                        RpcCall.Create<IScriptServiceV3Alpha>(nameof(IScriptServiceV3Alpha.StartScript)),
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
                scriptStatusResponse = new ScriptStatusResponseV3Alpha(
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

        protected override async Task<ScriptStatusResponseV3Alpha> GetStatus(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken cancellationToken)
        {
            try
            {
                async Task<ScriptStatusResponseV3Alpha> GetStatusAction(CancellationToken ct)
                {
                    var request = new ScriptStatusRequestV3Alpha(lastStatusResponse.ScriptTicket, lastStatusResponse.NextLogSequence);

                    return await clientScriptServiceV3Alpha.GetStatusAsync(request, new HalibutProxyRequestOptions(ct, CancellationToken.None));
                }

                if (ClientOptions.RpcRetrySettings.RetriesEnabled)
                {
                    return await rpcCallExecutor.ExecuteWithRetries(
                        RpcCall.Create<IScriptServiceV3Alpha>(nameof(IScriptServiceV3Alpha.GetStatus)),
                        GetStatusAction,
                        logger,
                        // If cancelling script execution we can abandon a call to GetStatus and go straight into the CancelScript and CompleteScript flow
                        abandonActionOnCancellation: true,
                        clientOperationMetricsBuilder,
                        cancellationToken).ConfigureAwait(false);
                }

                return await rpcCallExecutor.ExecuteWithNoRetries(
                    RpcCall.Create<IScriptServiceV3Alpha>(nameof(IScriptServiceV3Alpha.GetStatus)),
                    GetStatusAction,
                    logger,
                    abandonActionOnCancellation: true,
                    clientOperationMetricsBuilder,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                // Return the last known response without logs when cancellation occurs and let the script execution go into the CancelScript and CompleteScript flow
                return new ScriptStatusResponseV3Alpha(lastStatusResponse.ScriptTicket, lastStatusResponse.State, lastStatusResponse.ExitCode, new List<ProcessOutput>(), lastStatusResponse.NextLogSequence);
            }
        }

        protected override async Task<ScriptStatusResponseV3Alpha> Cancel(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken cancellationToken)
        {
            async Task<ScriptStatusResponseV3Alpha> CancelScriptAction(CancellationToken ct)
            {
                var request = new CancelScriptCommandV3Alpha(lastStatusResponse.ScriptTicket, lastStatusResponse.NextLogSequence);

                return await clientScriptServiceV3Alpha.CancelScriptAsync(request, new HalibutProxyRequestOptions(ct, CancellationToken.None));
            }

            if (ClientOptions.RpcRetrySettings.RetriesEnabled)
            {
                return await rpcCallExecutor.ExecuteWithRetries(
                    RpcCall.Create<IScriptServiceV3Alpha>(nameof(IScriptServiceV3Alpha.CancelScript)),
                    CancelScriptAction,
                    logger,
                    // We don't want to abandon this operation as it is responsible for stopping the script executing on the Tentacle
                    abandonActionOnCancellation: false,
                    clientOperationMetricsBuilder,
                    cancellationToken).ConfigureAwait(false);
            }

            return await rpcCallExecutor.ExecuteWithNoRetries(
                RpcCall.Create<IScriptServiceV3Alpha>(nameof(IScriptServiceV3Alpha.CancelScript)),
                CancelScriptAction,
                logger,
                abandonActionOnCancellation: false,
                clientOperationMetricsBuilder,
                cancellationToken).ConfigureAwait(false);
        }

        protected override async Task<ScriptStatusResponseV3Alpha> Finish(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            // Best effort cleanup of Tentacle
            try
            {
                var actionTask =
                    rpcCallExecutor.ExecuteWithNoRetries(
                        RpcCall.Create<IScriptServiceV3Alpha>(nameof(IScriptServiceV3Alpha.CompleteScript)),
                        async ct =>
                        {
                            var request = new CompleteScriptCommandV3Alpha(lastStatusResponse.ScriptTicket);

                            await clientScriptServiceV3Alpha.CompleteScriptAsync(request, new HalibutProxyRequestOptions(ct, CancellationToken.None));
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