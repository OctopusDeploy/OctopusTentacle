using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts
{
    public sealed class ObservingScriptOrchestrator<TStartCommand, TScriptStatusResponse> : IScriptOrchestrator
    {
        readonly IScriptObserverBackoffStrategy scriptObserverBackOffStrategy;
        readonly OnScriptStatusResponseReceived onScriptStatusResponseReceived;
        readonly OnScriptCompleted onScriptCompleted;

        IStructuredScriptOrchestrator<TStartCommand, TScriptStatusResponse> structuredScriptOrchestrator;

        public ObservingScriptOrchestrator(
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            IStructuredScriptOrchestrator<TStartCommand, TScriptStatusResponse> structuredScriptOrchestrator)
        {
            this.structuredScriptOrchestrator = structuredScriptOrchestrator;
            this.scriptObserverBackOffStrategy = scriptObserverBackOffStrategy;
            this.onScriptStatusResponseReceived = onScriptStatusResponseReceived;
            this.onScriptCompleted = onScriptCompleted;
        }

        public async Task<ScriptExecutionResult> ExecuteScript(ExecuteScriptCommand command, CancellationToken scriptExecutionCancellationToken)
        {
            var mappedStartCommand = structuredScriptOrchestrator.Map(command);

            var scriptStatusResponse = await structuredScriptOrchestrator.StartScript(mappedStartCommand, scriptExecutionCancellationToken).ConfigureAwait(false);

            scriptStatusResponse = await ObserveUntilCompleteThenFinish(scriptStatusResponse, scriptExecutionCancellationToken).ConfigureAwait(false);

            if (scriptExecutionCancellationToken.IsCancellationRequested)
            {
                // Throw an error so the caller knows that execution of the script was cancelled
                throw new OperationCanceledException("Script execution was cancelled");
            }

            var mappedResponse = structuredScriptOrchestrator.MapToResult(scriptStatusResponse);

            return new ScriptExecutionResult(mappedResponse.State, mappedResponse.ExitCode);
        }

        async Task<TScriptStatusResponse> ObserveUntilCompleteThenFinish(
            TScriptStatusResponse scriptStatusResponse,
            CancellationToken scriptExecutionCancellationToken)
        {
            OnScriptStatusResponseReceived(scriptStatusResponse);

            var lastScriptStatus = await ObserveUntilComplete(scriptStatusResponse, scriptExecutionCancellationToken).ConfigureAwait(false);

            await onScriptCompleted(scriptExecutionCancellationToken).ConfigureAwait(false);

            lastScriptStatus = await structuredScriptOrchestrator.Finish(lastScriptStatus, scriptExecutionCancellationToken).ConfigureAwait(false);
            
            OnScriptStatusResponseReceived(lastScriptStatus);

            return lastScriptStatus;
        }

        async Task<TScriptStatusResponse> ObserveUntilComplete(
            TScriptStatusResponse scriptStatusResponse,
            CancellationToken scriptExecutionCancellationToken)
        {
            var lastStatusResponse = scriptStatusResponse;
            var iteration = 0;
            var cancellationIteration = 0;

            while (structuredScriptOrchestrator.GetState(lastStatusResponse) != ProcessState.Complete)
            {
                if (scriptExecutionCancellationToken.IsCancellationRequested)
                {
                    lastStatusResponse = await structuredScriptOrchestrator.Cancel(lastStatusResponse, scriptExecutionCancellationToken).ConfigureAwait(false);
                }
                else
                {
                    try
                    {
                        lastStatusResponse = await structuredScriptOrchestrator.GetStatus(lastStatusResponse, scriptExecutionCancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        if (scriptExecutionCancellationToken.IsCancellationRequested)
                        {
                            continue; // Enter cancellation mode.
                        }

                        throw;
                    }
                }

                OnScriptStatusResponseReceived(lastStatusResponse);

                if (structuredScriptOrchestrator.GetState(lastStatusResponse) == ProcessState.Complete)
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



        protected void OnScriptStatusResponseReceived(TScriptStatusResponse scriptStatusResponse)
        {
            onScriptStatusResponseReceived(structuredScriptOrchestrator.MapToStatus(scriptStatusResponse));
        }
    }
}