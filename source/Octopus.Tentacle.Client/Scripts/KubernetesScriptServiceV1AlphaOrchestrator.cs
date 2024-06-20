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
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1Alpha;
using Octopus.Tentacle.Contracts.Logging;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Client.Scripts
{
    class KubernetesScriptServiceV1AlphaOrchestrator : IStructuredScriptOrchestrator<KubernetesScriptStatusResponseV1Alpha>
    {
        readonly IAsyncClientKubernetesScriptServiceV1Alpha clientKubernetesScriptServiceV1Alpha;
        readonly RpcCallExecutor rpcCallExecutor;
        readonly ClientOperationMetricsBuilder clientOperationMetricsBuilder;
        readonly TimeSpan onCancellationAbandonCompleteScriptAfter;
        readonly ITentacleClientTaskLog logger;
        readonly TentacleClientOptions clientOptions;

        public KubernetesScriptServiceV1AlphaOrchestrator(
            IAsyncClientKubernetesScriptServiceV1Alpha clientKubernetesScriptServiceV1Alpha,
            RpcCallExecutor rpcCallExecutor,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            TimeSpan onCancellationAbandonCompleteScriptAfter,
            TentacleClientOptions clientOptions,
            ITentacleClientTaskLog logger)
        {
            this.clientKubernetesScriptServiceV1Alpha = clientKubernetesScriptServiceV1Alpha;
            this.rpcCallExecutor = rpcCallExecutor;
            this.clientOperationMetricsBuilder = clientOperationMetricsBuilder;
            this.onCancellationAbandonCompleteScriptAfter = onCancellationAbandonCompleteScriptAfter;
            this.logger = logger;
            this.clientOptions = clientOptions;
        }

        StartKubernetesScriptCommandV1Alpha Map(ExecuteScriptCommand command)
        {
            if (command is not ExecuteKubernetesScriptCommand kubernetesScriptCommand)
                throw new InvalidOperationException($"Invalid execute script command received. Expected {nameof(ExecuteKubernetesScriptCommand)}, but received {command.GetType().Name}.");

            var podImageConfiguration = kubernetesScriptCommand.ImageConfiguration is not null
                ? new PodImageConfiguration(
                    kubernetesScriptCommand.ImageConfiguration.Image,
                    kubernetesScriptCommand.ImageConfiguration.FeedUrl,
                    kubernetesScriptCommand.ImageConfiguration.FeedUsername,
                    kubernetesScriptCommand.ImageConfiguration.FeedPassword)
                : null;

            return new StartKubernetesScriptCommandV1Alpha(
                kubernetesScriptCommand.ScriptTicket,
                kubernetesScriptCommand.TaskId,
                kubernetesScriptCommand.ScriptBody,
                kubernetesScriptCommand.Arguments,
                kubernetesScriptCommand.IsolationConfiguration.IsolationLevel,
                kubernetesScriptCommand.IsolationConfiguration.MutexTimeout,
                kubernetesScriptCommand.IsolationConfiguration.MutexName,
                podImageConfiguration,
                kubernetesScriptCommand.ScriptPodServiceAccountName,
                kubernetesScriptCommand.Scripts,
                kubernetesScriptCommand.Files.ToArray());
        }

        public ScriptExecutionStatus MapToStatus(KubernetesScriptStatusResponseV1Alpha response)
            => new(response.Logs);

        public ScriptExecutionResult MapToResult(KubernetesScriptStatusResponseV1Alpha response)
            => new(response.State, response.ExitCode);

        public ProcessState GetState(KubernetesScriptStatusResponseV1Alpha response) => response.State;

        public async Task<KubernetesScriptStatusResponseV1Alpha> StartScript(ExecuteScriptCommand executeScriptCommand, CancellationToken scriptExecutionCancellationToken)
        {
            var command = Map(executeScriptCommand);
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
                    retriesEnabled: clientOptions.RpcRetrySettings.RetriesEnabled,
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
                        new ShortCutTakenHere();
                        //await ObserveUntilCompleteThenFinish(scriptStatusResponse, scriptExecutionCancellationToken).ConfigureAwait(false);
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

        public async Task<KubernetesScriptStatusResponseV1Alpha?> GetStatus(KubernetesScriptStatusResponseV1Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
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
                    retriesEnabled: clientOptions.RpcRetrySettings.RetriesEnabled,
                    RpcCall.Create<IKubernetesScriptServiceV1Alpha>(nameof(IKubernetesScriptServiceV1Alpha.GetStatus)),
                    GetStatusAction,
                    logger,
                    clientOperationMetricsBuilder,
                    scriptExecutionCancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException && scriptExecutionCancellationToken.IsCancellationRequested)
            {
                return null;
            }
        }

        public async Task<KubernetesScriptStatusResponseV1Alpha> Cancel(KubernetesScriptStatusResponseV1Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
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
                retriesEnabled: clientOptions.RpcRetrySettings.RetriesEnabled,
                RpcCall.Create<IKubernetesScriptServiceV1Alpha>(nameof(IKubernetesScriptServiceV1Alpha.CancelScript)),
                CancelScriptAction,
                logger,
                clientOperationMetricsBuilder,
                // We don't want to cancel this operation as it is responsible for stopping the script executing on the Tentacle
                CancellationToken.None).ConfigureAwait(false);
        }

        public async Task<KubernetesScriptStatusResponseV1Alpha> Finish(KubernetesScriptStatusResponseV1Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
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