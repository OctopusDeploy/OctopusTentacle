using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Halibut.Util;
using Octopus.Tentacle.Client.Capabilities;
using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Client.Services;
using Octopus.Tentacle.Client.Utils;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.Observability;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using ILog = Octopus.Diagnostics.ILog;

namespace Octopus.Tentacle.Client.Scripts
{
    internal class ScriptExecutionOrchestrator : IDisposable
    {
        readonly IScriptObserverBackoffStrategy scriptObserverBackOffStrategy;
        readonly RpcCallExecutor rpcCallExecutor;
        readonly ClientOperationMetricsBuilder clientOperationMetricsBuilder;
        readonly StartScriptCommandV2 startScriptCommand;
        readonly Action<ScriptStatusResponseV2> onScriptStatusResponseReceived;
        readonly Func<CancellationToken, Task> onScriptCompleted;
        readonly TimeSpan onCancellationAbandonCompleteScriptAfter;
        readonly RpcRetrySettings rpcRetrySettings;
        readonly AsyncHalibutFeature asyncHalibutFeature;
        readonly ILog logger;
        
        readonly SyncAndAsyncClientScriptServiceV1 clientScriptServiceV1;
        readonly SyncAndAsyncClientScriptServiceV2 clientScriptServiceV2;
        readonly SyncAndAsyncClientCapabilitiesServiceV2 clientCapabilitiesServiceV2;

        public ScriptExecutionOrchestrator(
            SyncAndAsyncClientScriptServiceV1 clientScriptServiceV1,
            SyncAndAsyncClientScriptServiceV2 clientScriptServiceV2,
            SyncAndAsyncClientCapabilitiesServiceV2 clientCapabilitiesServiceV2,
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            RpcCallExecutor rpcCallExecutor,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            StartScriptCommandV2 startScriptCommand,
            Action<ScriptStatusResponseV2> onScriptStatusResponseReceived,
            Func<CancellationToken, Task> onScriptCompleted,
            TimeSpan onCancellationAbandonCompleteScriptAfter,
            RpcRetrySettings rpcRetrySettings,
            AsyncHalibutFeature asyncHalibutFeature,
            ILog logger)
        {
            this.clientScriptServiceV1 = clientScriptServiceV1;
            this.clientScriptServiceV2 = clientScriptServiceV2;
            this.clientCapabilitiesServiceV2 = clientCapabilitiesServiceV2;
            this.scriptObserverBackOffStrategy = scriptObserverBackOffStrategy;
            this.rpcCallExecutor = rpcCallExecutor;
            this.clientOperationMetricsBuilder = clientOperationMetricsBuilder;
            this.startScriptCommand = startScriptCommand;
            this.onScriptStatusResponseReceived = onScriptStatusResponseReceived;
            this.onScriptCompleted = onScriptCompleted;
            this.onCancellationAbandonCompleteScriptAfter = onCancellationAbandonCompleteScriptAfter;
            this.rpcRetrySettings = rpcRetrySettings;
            this.asyncHalibutFeature = asyncHalibutFeature;
            this.logger = logger;
        }

        public async Task<ScriptStatusResponseV2> ExecuteScript(CancellationToken scriptExecutionCancellationToken)
        {
            ScriptStatusResponseV2 scriptStatusResponse;
            ScriptServiceVersion scriptServiceVersionToUse;
            var startScriptCallCount = 0;

            try
            {
                scriptServiceVersionToUse = await DetermineScriptServiceVersionToUse(scriptExecutionCancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (scriptExecutionCancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Script execution was cancelled");
            }

            if (scriptServiceVersionToUse == ScriptServiceVersion.Version2)
            {
                try
                {
                    async Task<ScriptStatusResponseV2> StartScriptAction(CancellationToken ct)
                    {
                        ++startScriptCallCount;
                        
                        var result = await asyncHalibutFeature
                            .WhenDisabled(() => clientScriptServiceV2.SyncService.StartScript(startScriptCommand, new HalibutProxyRequestOptions(ct, CancellationToken.None)))
                            .WhenEnabled(async () => await clientScriptServiceV2.AsyncService.StartScriptAsync(startScriptCommand, new HalibutProxyRequestOptions(ct, CancellationToken.None)));

                        return result;
                    }

                    if (rpcRetrySettings.RetriesEnabled)
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
                        startScriptCommand.ScriptTicket,
                        ProcessState.Pending,
                        ScriptExitCodes.RunningExitCode,
                        new List<ProcessOutput>(),
                        0);

                    await ObserveUntilCompleteThenFinish(scriptServiceVersionToUse, scriptStatusResponse, scriptExecutionCancellationToken).ConfigureAwait(false);

                    // Throw an error so the caller knows that execution of the script was cancelled
                    throw new OperationCanceledException("Script execution was cancelled");
                }
            }
            else
            {
                var startScriptCommandV1 = Map(startScriptCommand);

                var scriptTicket = await rpcCallExecutor.ExecuteWithNoRetries(
                    RpcCall.Create<IScriptService>(nameof(IScriptService.StartScript)),
                    async ct =>
                    {
                        var result = await asyncHalibutFeature
                            .WhenDisabled(() => clientScriptServiceV1.SyncService.StartScript(startScriptCommandV1, new HalibutProxyRequestOptions(ct, CancellationToken.None)))
                            .WhenEnabled(async () => await clientScriptServiceV1.AsyncService.StartScriptAsync(startScriptCommandV1, new HalibutProxyRequestOptions(ct, CancellationToken.None)));

                        return result;
                    },
                    logger,
                    abandonActionOnCancellation: false,
                    clientOperationMetricsBuilder,
                    scriptExecutionCancellationToken).ConfigureAwait(false);

                scriptStatusResponse = Map(scriptTicket);
            }

            var response = await ObserveUntilCompleteThenFinish(scriptServiceVersionToUse, scriptStatusResponse, scriptExecutionCancellationToken).ConfigureAwait(false);

            if (scriptExecutionCancellationToken.IsCancellationRequested)
            {
                // Throw an error so the caller knows that execution of the script was cancelled
                throw new OperationCanceledException("Script execution was cancelled");
            }

            return response;
        }

        async Task<ScriptServiceVersion> DetermineScriptServiceVersionToUse(CancellationToken cancellationToken)
        {
            logger.Verbose("Determining ScriptService version to use");

            CapabilitiesResponseV2 tentacleCapabilities;

            async Task<CapabilitiesResponseV2> GetCapabilitiesFunc(CancellationToken ct)
            {
                var result = await asyncHalibutFeature
                    .WhenDisabled(() => clientCapabilitiesServiceV2.SyncService.GetCapabilities(new HalibutProxyRequestOptions(ct, CancellationToken.None)))
                    .WhenEnabled(async () => await clientCapabilitiesServiceV2.AsyncService.GetCapabilitiesAsync(new HalibutProxyRequestOptions(ct, CancellationToken.None)));

                return result;
            }

            if (rpcRetrySettings.RetriesEnabled)
            {
                tentacleCapabilities = await rpcCallExecutor.ExecuteWithRetries(
                    RpcCall.Create<ICapabilitiesServiceV2>(nameof(ICapabilitiesServiceV2.GetCapabilities)),
                    GetCapabilitiesFunc,
                    logger,
                    // We can abandon a call to Get Capabilities and walk away as this is not running anything that needs to be cancelled on Tentacle
                    abandonActionOnCancellation: true,
                    clientOperationMetricsBuilder,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                tentacleCapabilities = await rpcCallExecutor.ExecuteWithNoRetries(
                    RpcCall.Create<ICapabilitiesServiceV2>(nameof(ICapabilitiesServiceV2.GetCapabilities)),
                    GetCapabilitiesFunc,
                    logger,
                    abandonActionOnCancellation: true,
                    clientOperationMetricsBuilder,
                    cancellationToken).ConfigureAwait(false);
            }

            logger.Verbose($"Discovered Tentacle capabilities: {string.Join(",", tentacleCapabilities.SupportedCapabilities)}");

            if (tentacleCapabilities.HasScriptServiceV2())
            {
                logger.Verbose("Using ScriptServiceV2");
                logger.Verbose(rpcRetrySettings.RetriesEnabled
                    ? $"RPC call retries are enabled. Retry timeout {rpcCallExecutor.RetryTimeout.TotalSeconds} seconds"
                    : "RPC call retries are disabled.");
                return ScriptServiceVersion.Version2;
            }

            logger.Verbose("RPC call retries are enabled but will not be used for Script Execution as a compatible ScriptService was not found. Please upgrade Tentacle to enable this feature.");
            logger.Verbose("Using ScriptServiceV1");
            return ScriptServiceVersion.Version1;
        }

        async Task<ScriptStatusResponseV2> ObserveUntilCompleteThenFinish(
            ScriptServiceVersion scriptServiceVersionToUse,
            ScriptStatusResponseV2 scriptStatusResponse,
            CancellationToken scriptExecutionCancellationToken)
        {
            onScriptStatusResponseReceived(scriptStatusResponse);

            var lastScriptStatus = await ObserveUntilComplete(scriptServiceVersionToUse, scriptStatusResponse, scriptExecutionCancellationToken).ConfigureAwait(false);

            await onScriptCompleted(scriptExecutionCancellationToken).ConfigureAwait(false);

            lastScriptStatus = await Finish(scriptServiceVersionToUse, lastScriptStatus, scriptExecutionCancellationToken).ConfigureAwait(false);

            return lastScriptStatus;
        }

        async Task<ScriptStatusResponseV2> ObserveUntilComplete(
            ScriptServiceVersion scriptServiceVersionToUse,
            ScriptStatusResponseV2 scriptStatusResponse,
            CancellationToken scriptExecutionCancellationToken)
        {
            var lastStatusResponse = scriptStatusResponse;
            var iteration = 0;
            var cancellationIteration = 0;

            while (lastStatusResponse.State != ProcessState.Complete)
            {
                if (scriptExecutionCancellationToken.IsCancellationRequested)
                {
                    lastStatusResponse = await Cancel(scriptServiceVersionToUse, lastStatusResponse).ConfigureAwait(false);
                }
                else
                {
                    try
                    {
                        lastStatusResponse = await GetStatus(scriptServiceVersionToUse, lastStatusResponse, scriptExecutionCancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        if (scriptExecutionCancellationToken.IsCancellationRequested) continue; // Enter cancellation mode.
                        throw;
                    }
                }

                onScriptStatusResponseReceived(lastStatusResponse);

                if (lastStatusResponse.State == ProcessState.Complete)
                {
                    continue;
                }

                if (scriptExecutionCancellationToken.IsCancellationRequested)
                {
                    // When cancelling we want to back-off between checks to see if the script has cancelled but restart from iteration 0
                    await Task.Delay(scriptObserverBackOffStrategy.GetBackoff(++cancellationIteration), CancellationToken.None)
                        .ConfigureAwait(false);
                }
                else
                {
                    await Task.Delay(scriptObserverBackOffStrategy.GetBackoff(++iteration), scriptExecutionCancellationToken)
                        .SuppressOperationCanceledException()
                        .ConfigureAwait(false);
                }
            }

            return lastStatusResponse;
        }

        async Task<ScriptStatusResponseV2> GetStatus(
            ScriptServiceVersion scriptServiceVersionToUse,
            ScriptStatusResponseV2 lastStatusResponse,
            CancellationToken cancellationToken)
        {
            if (scriptServiceVersionToUse == ScriptServiceVersion.Version2)
            {
                try
                {
                    async Task<ScriptStatusResponseV2> GetStatusAction(CancellationToken ct)
                    {
                        var request = new ScriptStatusRequestV2(lastStatusResponse.Ticket, lastStatusResponse.NextLogSequence);

                        var result = await asyncHalibutFeature
                            .WhenDisabled(() => clientScriptServiceV2.SyncService.GetStatus(request, new HalibutProxyRequestOptions(ct, CancellationToken.None)))
                            .WhenEnabled(async () => await clientScriptServiceV2.AsyncService.GetStatusAsync(request, new HalibutProxyRequestOptions(ct, CancellationToken.None)));

                        return result;
                    }

                    if (rpcRetrySettings.RetriesEnabled)
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
                    else
                    {
                        return await rpcCallExecutor.ExecuteWithNoRetries(
                            RpcCall.Create<IScriptServiceV2>(nameof(IScriptServiceV2.GetStatus)),
                            GetStatusAction,
                            logger,
                            abandonActionOnCancellation: true,
                            clientOperationMetricsBuilder,
                            cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception e) when (e is OperationCanceledException && cancellationToken.IsCancellationRequested)
                {
                    // Return the last known response without logs when cancellation occurs and let the script execution go into the CancelScript and CompleteScript flow
                    return new ScriptStatusResponseV2(lastStatusResponse.Ticket, lastStatusResponse.State, lastStatusResponse.ExitCode, new List<ProcessOutput>(), lastStatusResponse.NextLogSequence);
                }
            }
            else
            {
                var scriptStatusResponseV1 = await rpcCallExecutor.ExecuteWithNoRetries(
                    RpcCall.Create<IScriptService>(nameof(IScriptService.GetStatus)),
                    async ct =>
                    {
                        var request = new ScriptStatusRequest(lastStatusResponse.Ticket, lastStatusResponse.NextLogSequence);

                        var result = await asyncHalibutFeature
                            .WhenDisabled(() => clientScriptServiceV1.SyncService.GetStatus(request, new HalibutProxyRequestOptions(ct, CancellationToken.None)))
                            .WhenEnabled(async () => await clientScriptServiceV1.AsyncService.GetStatusAsync(request, new HalibutProxyRequestOptions(ct, CancellationToken.None)));

                        return result;
                    },
                    logger,
                    abandonActionOnCancellation: false,
                    clientOperationMetricsBuilder,
                    cancellationToken).ConfigureAwait(false);

                return Map(scriptStatusResponseV1);
            }
        }

        async Task<ScriptStatusResponseV2> Cancel(
            ScriptServiceVersion scriptServiceVersionToUse,
            ScriptStatusResponseV2 lastStatusResponse)
        {
            // We don't want to cancel this operation as it is responsible for stopping the script executing on the Tentacle
            var cancellationToken = CancellationToken.None;
            if (scriptServiceVersionToUse == ScriptServiceVersion.Version2)
            {
                async Task<ScriptStatusResponseV2> CancelScriptAction(CancellationToken ct)
                {
                    var request = new CancelScriptCommandV2(lastStatusResponse.Ticket, lastStatusResponse.NextLogSequence);

                    var result = await asyncHalibutFeature
                        .WhenDisabled(() => clientScriptServiceV2.SyncService.CancelScript(request, new HalibutProxyRequestOptions(ct, CancellationToken.None)))
                        .WhenEnabled(async () => await clientScriptServiceV2.AsyncService.CancelScriptAsync(request, new HalibutProxyRequestOptions(ct, CancellationToken.None)));

                    return result;
                }

                if (rpcRetrySettings.RetriesEnabled)
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
                else
                {
                    return await rpcCallExecutor.ExecuteWithNoRetries(
                        RpcCall.Create<IScriptServiceV2>(nameof(IScriptServiceV2.CancelScript)),
                        CancelScriptAction,
                        logger,
                        abandonActionOnCancellation: false,
                        clientOperationMetricsBuilder,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                var scriptStatusResponseV1 = await rpcCallExecutor.ExecuteWithNoRetries(
                    RpcCall.Create<IScriptService>(nameof(IScriptService.CancelScript)),
                    async ct =>
                    {
                        var request = new CancelScriptCommand(lastStatusResponse.Ticket, lastStatusResponse.NextLogSequence);

                        var result = await asyncHalibutFeature
                            .WhenDisabled(() => clientScriptServiceV1.SyncService.CancelScript(request, new HalibutProxyRequestOptions(ct, CancellationToken.None)))
                            .WhenEnabled(async () => await clientScriptServiceV1.AsyncService.CancelScriptAsync(request, new HalibutProxyRequestOptions(ct, CancellationToken.None)));

                        return result;
                    },
                    logger,
                    abandonActionOnCancellation: false,
                    clientOperationMetricsBuilder,
                    cancellationToken).ConfigureAwait(false);

                return Map(scriptStatusResponseV1);
            }
        }

        async Task<ScriptStatusResponseV2> Finish(
            ScriptServiceVersion scriptServiceVersionToUse,
            ScriptStatusResponseV2 lastStatusResponse,
            CancellationToken scriptExecutionCancellationToken)
        {
            ScriptStatusResponseV2 completeStatus;

            if (scriptServiceVersionToUse == ScriptServiceVersion.Version2)
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

                                await asyncHalibutFeature
                                    .WhenDisabled(() => clientScriptServiceV2.SyncService.CompleteScript(request, new HalibutProxyRequestOptions(ct, CancellationToken.None)))
                                    .WhenEnabled(async () => await clientScriptServiceV2.AsyncService.CompleteScriptAsync(request, new HalibutProxyRequestOptions(ct, CancellationToken.None)));
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

                completeStatus = lastStatusResponse;
            }
            else
            {
                var completeStatusV1 = await rpcCallExecutor.ExecuteWithNoRetries(
                    RpcCall.Create<IScriptService>(nameof(IScriptService.CompleteScript)),
                    async ct =>
                    {
                        var request = new CompleteScriptCommand(lastStatusResponse.Ticket, lastStatusResponse.NextLogSequence);

                        var result = await asyncHalibutFeature
                            .WhenDisabled(() => clientScriptServiceV1.SyncService.CompleteScript(request, new HalibutProxyRequestOptions(ct, CancellationToken.None)))
                            .WhenEnabled(async () => await clientScriptServiceV1.AsyncService.CompleteScriptAsync(request, new HalibutProxyRequestOptions(ct, CancellationToken.None)));

                        return result;
                    },
                    logger,
                    abandonActionOnCancellation: false,
                    clientOperationMetricsBuilder,
                    CancellationToken.None).ConfigureAwait(false);

                completeStatus = Map(completeStatusV1);
                onScriptStatusResponseReceived(completeStatus);
            }

            return completeStatus;
        }

        static ScriptStatusResponseV2 Map(ScriptTicket ticket)
        {
            return new ScriptStatusResponseV2(ticket,
                ProcessState.Pending,
                0,
                new List<ProcessOutput>(),
                0);
        }

        static ScriptStatusResponseV2 Map(ScriptStatusResponse scriptStatusResponse)
        {
            return new ScriptStatusResponseV2(scriptStatusResponse.Ticket,
                scriptStatusResponse.State,
                scriptStatusResponse.ExitCode,
                scriptStatusResponse.Logs,
                scriptStatusResponse.NextLogSequence);
        }

        static StartScriptCommand Map(StartScriptCommandV2 command)
        {
            return new StartScriptCommand(
                command.ScriptBody,
                command.Isolation,
                command.ScriptIsolationMutexTimeout,
                command.IsolationMutexName!,
                command.Arguments,
                command.TaskId,
                command.Scripts,
                command.Files?.ToArray() ?? Array.Empty<ScriptFile>());
        }

        public void Dispose()
        {
        }
    }
}
