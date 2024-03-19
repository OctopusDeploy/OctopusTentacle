using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Scripts.Models;
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

        protected override StartScriptCommandV3Alpha Map(ExecuteScriptCommand command)
        {
            IScriptExecutionContext executionContext = command switch
            {
                ExecuteKubernetesScriptCommand kubernetesScriptCommand => new KubernetesAgentScriptExecutionContext(kubernetesScriptCommand.Image, kubernetesScriptCommand.FeedUrl, kubernetesScriptCommand.FeedUsername, kubernetesScriptCommand.FeedPassword),
                _ => new LocalShellScriptExecutionContext()
            };

            return new StartScriptCommandV3Alpha(
                command.ScriptBody,
                command.IsolationLevel,
                command.IsolationMutexTimeout,
                command.IsolationMutexName!,
                command.Arguments,
                command.TaskId,
                command.ScriptTicket,
                command.DurationToWaitForScriptToFinish,
                executionContext,
                command.Scripts,
                command.Files.ToArray());
        }

        protected override ScriptExecutionStatus MapToStatus(ScriptStatusResponseV3Alpha response)
            => new(response.Logs);

        protected override ScriptExecutionResult MapToResult(ScriptStatusResponseV3Alpha response)
            => new(response.State, response.ExitCode);

        protected override ProcessState GetState(ScriptStatusResponseV3Alpha response) => response.State;

        protected override async Task<ScriptStatusResponseV3Alpha> StartScript(StartScriptCommandV3Alpha command, CancellationToken scriptExecutionCancellationToken)
        {
            ScriptStatusResponseV3Alpha scriptStatusResponse;
            var startScriptCallsConnectedCount = 0;
            try
            {
                async Task<ScriptStatusResponseV3Alpha> StartScriptAction(CancellationToken ct)
                {
                    ++startScriptCallsConnectedCount;
                    var result = await clientScriptServiceV3Alpha.StartScriptAsync(command, new HalibutProxyRequestOptions(ct));

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
                    RpcCall.Create<IScriptServiceV3Alpha>(nameof(IScriptServiceV3Alpha.StartScript)),
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
                    scriptStatusResponse = new ScriptStatusResponseV3Alpha(
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

        protected override async Task<ScriptStatusResponseV3Alpha> GetStatus(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            try
            {
                async Task<ScriptStatusResponseV3Alpha> GetStatusAction(CancellationToken ct)
                {
                    var request = new ScriptStatusRequestV3Alpha(lastStatusResponse.ScriptTicket, lastStatusResponse.NextLogSequence);
                    var result = await clientScriptServiceV3Alpha.GetStatusAsync(request, new HalibutProxyRequestOptions(ct));

                    return result;
                }

                return await rpcCallExecutor.Execute(
                    retriesEnabled: ClientOptions.RpcRetrySettings.RetriesEnabled,
                    RpcCall.Create<IScriptServiceV3Alpha>(nameof(IScriptServiceV3Alpha.GetStatus)),
                    GetStatusAction,
                    logger,
                    clientOperationMetricsBuilder,
                    scriptExecutionCancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException && scriptExecutionCancellationToken.IsCancellationRequested)
            {
                // Return the last known response without logs when cancellation occurs and let the script execution go into the CancelScript and CompleteScript flow
                return new ScriptStatusResponseV3Alpha(lastStatusResponse.ScriptTicket, lastStatusResponse.State, lastStatusResponse.ExitCode, new List<ProcessOutput>(), lastStatusResponse.NextLogSequence);
            }
        }

        protected override async Task<ScriptStatusResponseV3Alpha> Cancel(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            async Task<ScriptStatusResponseV3Alpha> CancelScriptAction(CancellationToken ct)
            {
                var request = new CancelScriptCommandV3Alpha(lastStatusResponse.ScriptTicket, lastStatusResponse.NextLogSequence);
                var result = await clientScriptServiceV3Alpha.CancelScriptAsync(request, new HalibutProxyRequestOptions(ct));

                return result;
            }

            // TODO: SaST - This could be optimized for the failure scenario.
            // If script execution is already triggering RPC Retries and then the script execution is cancelled there is a high chance that the cancel RPC call will fail as well and go into RPC retries.
            // We could potentially reduce the time to failure by not retrying the cancel RPC Call if the previous RPC call was already triggering RPC Retries.

            return await rpcCallExecutor.Execute(
                retriesEnabled: ClientOptions.RpcRetrySettings.RetriesEnabled,
                RpcCall.Create<IScriptServiceV3Alpha>(nameof(IScriptServiceV3Alpha.CancelScript)),
                CancelScriptAction,
                logger,
                clientOperationMetricsBuilder,
                // We don't want to cancel this operation as it is responsible for stopping the script executing on the Tentacle
                CancellationToken.None).ConfigureAwait(false);
        }

        protected override async Task<ScriptStatusResponseV3Alpha> Finish(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            try
            {
                // Finish performs a best effort cleanup of the Workspace on Tentacle
                // If we are cancelling script execution we abandon a call to complete script after a period of time
                using var completeScriptCancellationTokenSource = new CancellationTokenSource();

                await using var _ = scriptExecutionCancellationToken.Register(() => completeScriptCancellationTokenSource.CancelAfter(onCancellationAbandonCompleteScriptAfter));

                await rpcCallExecutor.ExecuteWithNoRetries(
                        RpcCall.Create<IScriptServiceV3Alpha>(nameof(IScriptServiceV3Alpha.CompleteScript)),
                        async ct =>
                        {
                            var request = new CompleteScriptCommandV3Alpha(lastStatusResponse.ScriptTicket);
                            await clientScriptServiceV3Alpha.CompleteScriptAsync(request, new HalibutProxyRequestOptions(ct));
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