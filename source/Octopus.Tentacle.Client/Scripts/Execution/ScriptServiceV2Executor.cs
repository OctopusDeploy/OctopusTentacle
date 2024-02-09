using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using ILog = Octopus.Diagnostics.ILog;

namespace Octopus.Tentacle.Client.Scripts.Execution
{
    class ScriptServiceV2Executor : IScriptServiceExecutor
    {
        readonly IAsyncClientScriptServiceV2 clientScriptServiceV2;
        readonly RpcCallExecutor rpcCallExecutor;
        readonly ClientOperationMetricsBuilder clientOperationMetricsBuilder;
        readonly TimeSpan onCancellationAbandonCompleteScriptAfter;
        readonly TentacleClientOptions clientOptions;
        readonly ILog logger;

        public ScriptServiceV2Executor(
            IAsyncClientScriptServiceV2 clientScriptServiceV2,
            RpcCallExecutor rpcCallExecutor,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            TimeSpan onCancellationAbandonCompleteScriptAfter,
            TentacleClientOptions clientOptions,
            ILog logger)
        {
            this.clientScriptServiceV2 = clientScriptServiceV2;
            this.rpcCallExecutor = rpcCallExecutor;
            this.clientOperationMetricsBuilder = clientOperationMetricsBuilder;
            this.onCancellationAbandonCompleteScriptAfter = onCancellationAbandonCompleteScriptAfter;
            this.clientOptions = clientOptions;
            this.logger = logger;
        }

        public async Task<ScriptStatusResponseV3Alpha> StartScript(StartScriptCommandV3Alpha command, CancellationToken scriptExecutionCancellationToken)
        {
            ScriptStatusResponseV2 scriptStatusResponse;
            var startScriptCallsConnectedCount = 0;
            try
            {
                async Task<ScriptStatusResponseV2> StartScriptAction(CancellationToken ct)
                {
                    var startScriptCommandV2 = MapToStartScriptCommand(command);
                    ++startScriptCallsConnectedCount;
                    var result = await clientScriptServiceV2.StartScriptAsync(startScriptCommandV2, new HalibutProxyRequestOptions(ct));

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
                    throw new ScriptExecutionCancelledAfterPotentiallyStartingException("Script was cancelled after potentially starting");
                }

                // If the StartScript call was not in-flight or being retries then we know the script has not started executing on Tentacle
                // So can exit without calling CancelScript or CompleteScript
                throw new OperationCanceledException("Script execution was cancelled", ex);
            }

            return MapToLatestScriptStatusResponse(scriptStatusResponse);
        }

        public async Task<ScriptStatusResponseV3Alpha> GetStatus(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            return await GetStatus(lastStatusResponse.ScriptTicket, lastStatusResponse.NextLogSequence, scriptExecutionCancellationToken);
        }

        public async Task<ScriptStatusResponseV3Alpha> GetStatus(CommandContextV3Alpha commandContext, CancellationToken scriptExecutionCancellationToken)
        {
            return await GetStatus(commandContext.ScriptTicket, commandContext.NextLogSequence, scriptExecutionCancellationToken);
        }

        async Task<ScriptStatusResponseV3Alpha> GetStatus(ScriptTicket scriptTicket, long nextLogSequence, CancellationToken scriptExecutionCancellationToken)
        {
            async Task<ScriptStatusResponseV2> GetStatusAction(CancellationToken ct)
            {
                var request = new ScriptStatusRequestV2(scriptTicket, nextLogSequence);
                var result = await clientScriptServiceV2.GetStatusAsync(request, new HalibutProxyRequestOptions(ct));

                return result;
            }

            var scriptStatusResponseV2 = await rpcCallExecutor.Execute(
                retriesEnabled: clientOptions.RpcRetrySettings.RetriesEnabled,
                RpcCall.Create<IScriptServiceV2>(nameof(IScriptServiceV2.GetStatus)),
                GetStatusAction,
                logger,
                clientOperationMetricsBuilder,
                scriptExecutionCancellationToken).ConfigureAwait(false);

            return MapToLatestScriptStatusResponse(scriptStatusResponseV2);
        }

        public async Task<ScriptStatusResponseV3Alpha> Cancel(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            return await Cancel(lastStatusResponse.ScriptTicket, lastStatusResponse.NextLogSequence);
        }

        public async Task<ScriptStatusResponseV3Alpha> Cancel(CommandContextV3Alpha commandContext, CancellationToken scriptExecutionCancellationToken)
        {
            return await Cancel(commandContext.ScriptTicket, commandContext.NextLogSequence);
        }

        async Task<ScriptStatusResponseV3Alpha> Cancel(ScriptTicket scriptTicket, long nextLogSequence)
        {
            async Task<ScriptStatusResponseV2> CancelScriptAction(CancellationToken ct)
            {
                var request = new CancelScriptCommandV2(scriptTicket, nextLogSequence);
                var result = await clientScriptServiceV2.CancelScriptAsync(request, new HalibutProxyRequestOptions(ct));

                return result;
            }

            // TODO: SaST - This could be optimized for the failure scenario.
            // If script execution is already triggering RPC Retries and then the script execution is cancelled there is a high chance that the cancel RPC call will fail as well and go into RPC retries.
            // We could potentially reduce the time to failure by not retrying the cancel RPC Call if the previous RPC call was already triggering RPC Retries.

            var scriptStatusResponseV2 = await rpcCallExecutor.Execute(
                retriesEnabled: clientOptions.RpcRetrySettings.RetriesEnabled,
                RpcCall.Create<IScriptServiceV2>(nameof(IScriptServiceV2.CancelScript)),
                CancelScriptAction,
                logger,
                clientOperationMetricsBuilder,
                // We don't want to cancel this operation as it is responsible for stopping the script executing on the Tentacle
                CancellationToken.None).ConfigureAwait(false);

            return MapToLatestScriptStatusResponse(scriptStatusResponseV2);
        }

        public async Task<ScriptStatusResponseV3Alpha> Finish(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            await Finish(lastStatusResponse.ScriptTicket, scriptExecutionCancellationToken);

            return lastStatusResponse;
        }

        public async Task<ScriptStatusResponseV3Alpha> Finish(CommandContextV3Alpha commandContext, CancellationToken scriptExecutionCancellationToken)
        {
            await Finish(commandContext.ScriptTicket, scriptExecutionCancellationToken);

            return new ScriptStatusResponseV3Alpha(commandContext.ScriptTicket, ProcessState.Complete, 0, new List<ProcessOutput>(), commandContext.NextLogSequence);
        }

        async Task Finish(ScriptTicket scriptTicket, CancellationToken scriptExecutionCancellationToken)
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
                        var request = new CompleteScriptCommandV2(scriptTicket);
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

        static StartScriptCommandV2 MapToStartScriptCommand(StartScriptCommandV3Alpha command)
        {
            return new StartScriptCommandV2(
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
        }

        static ScriptStatusResponseV3Alpha MapToLatestScriptStatusResponse(ScriptStatusResponseV2 scriptStatusResponseV2)
        {
            return new ScriptStatusResponseV3Alpha(
                scriptStatusResponseV2.Ticket,
                scriptStatusResponseV2.State,
                scriptStatusResponseV2.ExitCode,
                scriptStatusResponseV2.Logs,
                scriptStatusResponseV2.NextLogSequence);
        }
    }
}