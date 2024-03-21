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
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1Alpha;
using ILog = Octopus.Diagnostics.ILog;

namespace Octopus.Tentacle.Client.Scripts
{
    class KubernetesScriptServiceV1AlphaOrchestrator : ObservingScriptOrchestrator<StartKubernetesScriptCommandV1Alpha, KubernetesScriptStatusResponseV1Alpha>
    {
        readonly IAsyncClientKubernetesScriptServiceV1Alpha clientKubernetesScriptServiceV1Alpha;
        readonly RpcCallExecutor rpcCallExecutor;
        readonly ClientOperationMetricsBuilder clientOperationMetricsBuilder;
        readonly TimeSpan onCancellationAbandonCompleteScriptAfter;
        readonly ILog logger;

        public KubernetesScriptServiceV1AlphaOrchestrator(
            IAsyncClientKubernetesScriptServiceV1Alpha clientKubernetesScriptServiceV1Alpha,
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
            this.clientKubernetesScriptServiceV1Alpha = clientKubernetesScriptServiceV1Alpha;
            this.rpcCallExecutor = rpcCallExecutor;
            this.clientOperationMetricsBuilder = clientOperationMetricsBuilder;
            this.onCancellationAbandonCompleteScriptAfter = onCancellationAbandonCompleteScriptAfter;
            this.logger = logger;
        }

        protected override StartKubernetesScriptCommandV1Alpha Map(ExecuteScriptCommand command)
        {
            if (command is not ExecuteKubernetesScriptCommand kubernetesScriptCommand)
                throw new InvalidOperationException($"Invalid execute script command received. Expected {nameof(ExecuteKubernetesScriptCommand)}, but received {command.GetType().Name}.");

            return new StartKubernetesScriptCommandV1Alpha(
                kubernetesScriptCommand.ScriptBody,
                kubernetesScriptCommand.IsolationConfiguration.IsolationLevel,
                kubernetesScriptCommand.IsolationConfiguration.MutexTimeout,
                kubernetesScriptCommand.IsolationConfiguration.MutexName,
                kubernetesScriptCommand.Arguments,
                kubernetesScriptCommand.TaskId,
                kubernetesScriptCommand.ScriptTicket,
                kubernetesScriptCommand.ImageConfiguration.Image,
                kubernetesScriptCommand.ImageConfiguration.FeedUrl,
                kubernetesScriptCommand.ImageConfiguration.FeedUsername,
                kubernetesScriptCommand.ImageConfiguration.FeedPassword
                kubernetesScriptCommand.Scripts,
                kubernetesScriptCommand.Files.ToArray());
        }

        protected override ScriptExecutionStatus MapToStatus(KubernetesScriptStatusResponseV1Alpha response)
            => new(response.Logs);

        protected override ScriptExecutionResult MapToResult(KubernetesScriptStatusResponseV1Alpha response)
            => new(response.State, response.ExitCode);

        protected override ProcessState GetState(KubernetesScriptStatusResponseV1Alpha response) => response.State;

        protected override async Task<KubernetesScriptStatusResponseV1Alpha> StartScript(StartKubernetesScriptCommandV1Alpha command, CancellationToken scriptExecutionCancellationToken)
        {
            KubernetesScriptStatusResponseV1Alpha scriptStatusResponse;
            var startScriptCallsConnectedCount = 0;
            try
            {
                async Task<KubernetesScriptStatusResponseV1Alpha> StartScriptAction(CancellationToken ct)
                {
                    ++startScriptCallsConnectedCount;
                    var result = await clientKubernetesScriptServiceV1Alpha.StartScriptAsync(command, new HalibutProxyRequestOptions(ct));

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
                    RpcCall.Create<IKubernetesScriptServiceV1Alpha>(nameof(IKubernetesScriptServiceV1Alpha.StartScript)),
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
                    scriptStatusResponse = new KubernetesScriptStatusResponseV1Alpha(
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

        protected override async Task<KubernetesScriptStatusResponseV1Alpha> GetStatus(KubernetesScriptStatusResponseV1Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            try
            {
                async Task<KubernetesScriptStatusResponseV1Alpha> GetStatusAction(CancellationToken ct)
                {
                    var request = new KubernetesScriptStatusRequestV1Alpha(lastStatusResponse.ScriptTicket, lastStatusResponse.NextLogSequence);
                    var result = await clientKubernetesScriptServiceV1Alpha.GetStatusAsync(request, new HalibutProxyRequestOptions(ct));

                    return result;
                }

                return await rpcCallExecutor.Execute(
                    retriesEnabled: ClientOptions.RpcRetrySettings.RetriesEnabled,
                    RpcCall.Create<IKubernetesScriptServiceV1Alpha>(nameof(IKubernetesScriptServiceV1Alpha.GetStatus)),
                    GetStatusAction,
                    logger,
                    clientOperationMetricsBuilder,
                    scriptExecutionCancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException && scriptExecutionCancellationToken.IsCancellationRequested)
            {
                // Return the last known response without logs when cancellation occurs and let the script execution go into the CancelScript and CompleteScript flow
                return new KubernetesScriptStatusResponseV1Alpha(lastStatusResponse.ScriptTicket, lastStatusResponse.State, lastStatusResponse.ExitCode, new List<ProcessOutput>(), lastStatusResponse.NextLogSequence);
            }
        }

        protected override async Task<KubernetesScriptStatusResponseV1Alpha> Cancel(KubernetesScriptStatusResponseV1Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            async Task<KubernetesScriptStatusResponseV1Alpha> CancelScriptAction(CancellationToken ct)
            {
                var request = new CancelKubernetesScriptCommandV1Alpha(lastStatusResponse.ScriptTicket, lastStatusResponse.NextLogSequence);
                var result = await clientKubernetesScriptServiceV1Alpha.CancelScriptAsync(request, new HalibutProxyRequestOptions(ct));

                return result;
            }

            // TODO: SaST - This could be optimized for the failure scenario.
            // If script execution is already triggering RPC Retries and then the script execution is cancelled there is a high chance that the cancel RPC call will fail as well and go into RPC retries.
            // We could potentially reduce the time to failure by not retrying the cancel RPC Call if the previous RPC call was already triggering RPC Retries.

            return await rpcCallExecutor.Execute(
                retriesEnabled: ClientOptions.RpcRetrySettings.RetriesEnabled,
                RpcCall.Create<IKubernetesScriptServiceV1Alpha>(nameof(IKubernetesScriptServiceV1Alpha.CancelScript)),
                CancelScriptAction,
                logger,
                clientOperationMetricsBuilder,
                // We don't want to cancel this operation as it is responsible for stopping the script executing on the Tentacle
                CancellationToken.None).ConfigureAwait(false);
        }

        protected override async Task<KubernetesScriptStatusResponseV1Alpha> Finish(KubernetesScriptStatusResponseV1Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            try
            {
                // Finish performs a best effort cleanup of the Workspace on Tentacle
                // If we are cancelling script execution we abandon a call to complete script after a period of time
                using var completeScriptCancellationTokenSource = new CancellationTokenSource();

                await using var _ = scriptExecutionCancellationToken.Register(() => completeScriptCancellationTokenSource.CancelAfter(onCancellationAbandonCompleteScriptAfter));

                await rpcCallExecutor.ExecuteWithNoRetries(
                        RpcCall.Create<IKubernetesScriptServiceV1Alpha>(nameof(IKubernetesScriptServiceV1Alpha.CompleteScript)),
                        async ct =>
                        {
                            var request = new CompleteKubernetesScriptCommandV1Alpha(lastStatusResponse.ScriptTicket);
                            await clientKubernetesScriptServiceV1Alpha.CompleteScriptAsync(request, new HalibutProxyRequestOptions(ct));
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