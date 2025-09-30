using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Logging;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts
{
    sealed class ObservingScriptOrchestrator
    {
        readonly IScriptObserverBackoffStrategy scriptObserverBackOffStrategy;
        readonly OnScriptStatusResponseReceived onScriptStatusResponseReceived;
        readonly OnScriptCompleted onScriptCompleted;
        readonly IScriptExecutor scriptExecutor;

        public ObservingScriptOrchestrator(
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            IScriptExecutor scriptExecutor)
        {
            this.scriptExecutor = scriptExecutor;
            this.scriptObserverBackOffStrategy = scriptObserverBackOffStrategy;
            this.onScriptStatusResponseReceived = onScriptStatusResponseReceived;
            this.onScriptCompleted = onScriptCompleted;
        }

        public async Task<ScriptExecutionResult> ExecuteScript(ExecuteScriptCommand command, CancellationToken scriptExecutionCancellationToken)
        {
            var startScriptResult = await scriptExecutor.StartScript(command,
                StartScriptIsBeingReAttempted.FirstAttempt, // This is not re-entrant so this should be true.
                scriptExecutionCancellationToken).ConfigureAwait(false);

            var scriptStatus = await ObserveUntilCompleteThenFinish(startScriptResult, scriptExecutionCancellationToken).ConfigureAwait(false);

            if (scriptExecutionCancellationToken.IsCancellationRequested)
            {
                // Throw an error so the caller knows that execution of the script was cancelled
                throw new OperationCanceledException("Script execution was cancelled");
            }

            return new ScriptExecutionResult(scriptStatus.State, scriptStatus.ExitCode);
        }

        async Task<ScriptStatus> ObserveUntilCompleteThenFinish(
            ScriptOperationExecutionResult startScriptResult,
            CancellationToken scriptExecutionCancellationToken)
        {
            using var activity = TentacleClient.ActivitySource.StartActivity($"{nameof(ObservingScriptOrchestrator)}.{nameof(ObserveUntilCompleteThenFinish)}");
            activity?.AddTag("octopus.tentacle.script.status.state", startScriptResult.ScriptStatus.State);
            activity?.AddTag("octopus.tentacle.script.status.exit_code", startScriptResult.ScriptStatus.ExitCode);
            
            OnScriptStatusResponseReceived(startScriptResult.ScriptStatus);

            var observingUntilCompleteResult =  await ObserveUntilComplete(startScriptResult, scriptExecutionCancellationToken).ConfigureAwait(false);

            await onScriptCompleted(scriptExecutionCancellationToken).ConfigureAwait(false);
            
            var completeScriptResponse = await scriptExecutor.CompleteScript(observingUntilCompleteResult.ContextForNextCommand, scriptExecutionCancellationToken).ConfigureAwait(false);

            // V1 can return a result when completing. But other versions do not.
            // The behaviour we are maintaining is that the result to use for V1 is that of "complete"
            // but the result to use for other versions is the last observing result.
            if (completeScriptResponse is not null)
            {
                // Because V1 can actually return a result, we need to handle the response received as well (so the output appears in Octopus Server)
                OnScriptStatusResponseReceived(completeScriptResponse);
                return completeScriptResponse;
            }
            
            return observingUntilCompleteResult.ScriptStatus;
        }

        async Task<ScriptOperationExecutionResult> ObserveUntilComplete(
            ScriptOperationExecutionResult startScriptResult,
            CancellationToken scriptExecutionCancellationToken)
        {
            var iteration = 0;
            var cancellationIteration = 0;
            var lastResult = startScriptResult;

            while (lastResult.ScriptStatus.State != ProcessState.Complete)
            {
                if (scriptExecutionCancellationToken.IsCancellationRequested)
                {
                    // We don't want to cancel this operation as it is responsible for stopping the script executing on the Tentacle
                    lastResult = await scriptExecutor.CancelScript(lastResult.ContextForNextCommand, CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    try
                    {
                        lastResult = await scriptExecutor.GetStatus(lastResult.ContextForNextCommand, scriptExecutionCancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        if (scriptExecutionCancellationToken.IsCancellationRequested)
                        {
                            continue; // Enter cancellation mode.
                        }
                        
                        // Before giving up, send out an attempt to cancel the inflight script.
                        // This can work around issues where the RPC retry duration is less than the timeout durations in halibut.
                        // In cases of short network issues, this means we  have a chance of canceling the in flight script.
                        var _ = Task.Run(() =>
                        {
                            try
                            {
                                scriptExecutor.CancelScript(lastResult.ContextForNextCommand);
                            }
                            catch (Exception)
                            {
                                // ignored
                            }
                        });

                        throw;
                    }
                }

                OnScriptStatusResponseReceived(lastResult.ScriptStatus);

                if (lastResult.ScriptStatus.State == ProcessState.Complete)
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

            return lastResult;
        }

        void OnScriptStatusResponseReceived(ScriptStatus scriptStatusResponse)
        {
            var scriptExecutionStatus = new ScriptExecutionStatus(scriptStatusResponse.Logs);
            onScriptStatusResponseReceived(scriptExecutionStatus);
        }
    }
}