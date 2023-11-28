using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Client.Scripts
{
    abstract class ObservingScriptOrchestrator<TStartCommand, TScriptStatusResponse> : IScriptOrchestrator
    {
        readonly IScriptObserverBackoffStrategy scriptObserverBackOffStrategy;
        readonly OnScriptStatusResponseReceived onScriptStatusResponseReceived;
        readonly OnScriptCompleted onScriptCompleted;

        protected TentacleClientOptions ClientOptions { get; }

        protected ObservingScriptOrchestrator(
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            TentacleClientOptions clientOptions)
        {
            ClientOptions = clientOptions;
            this.scriptObserverBackOffStrategy = scriptObserverBackOffStrategy;
            this.onScriptStatusResponseReceived = onScriptStatusResponseReceived;
            this.onScriptCompleted = onScriptCompleted;
        }

        public async Task<ScriptExecutionResult> ExecuteScript(StartScriptCommandV3Alpha startScriptCommand, CancellationToken scriptExecutionCancellationToken)
        {
            var mappedStartCommand = Map(startScriptCommand);

            var scriptStatusResponse = await StartScript(mappedStartCommand, scriptExecutionCancellationToken).ConfigureAwait(false);

            scriptStatusResponse = await ObserveUntilCompleteThenFinish(scriptStatusResponse, scriptExecutionCancellationToken).ConfigureAwait(false);

            if (scriptExecutionCancellationToken.IsCancellationRequested)
            {
                // Throw an error so the caller knows that execution of the script was cancelled
                throw new OperationCanceledException("Script execution was cancelled");
            }

            var mappedResponse = MapToResult(scriptStatusResponse);

            return new ScriptExecutionResult(mappedResponse.State, mappedResponse.ExitCode);
        }

        protected async Task<TScriptStatusResponse> ObserveUntilCompleteThenFinish(
            TScriptStatusResponse scriptStatusResponse,
            CancellationToken scriptExecutionCancellationToken)
        {
            OnScriptStatusResponseReceived(scriptStatusResponse);

            var lastScriptStatus = await ObserveUntilComplete(scriptStatusResponse, scriptExecutionCancellationToken).ConfigureAwait(false);

            await onScriptCompleted(scriptExecutionCancellationToken).ConfigureAwait(false);

            lastScriptStatus = await Finish(lastScriptStatus, scriptExecutionCancellationToken).ConfigureAwait(false);

            return lastScriptStatus;
        }

        async Task<TScriptStatusResponse> ObserveUntilComplete(
            TScriptStatusResponse scriptStatusResponse,
            CancellationToken scriptExecutionCancellationToken)
        {
            var lastStatusResponse = scriptStatusResponse;
            var iteration = 0;
            var cancellationIteration = 0;

            while (GetState(lastStatusResponse) != ProcessState.Complete)
            {
                if (scriptExecutionCancellationToken.IsCancellationRequested)
                {
                    // We don't want to cancel this operation as it is responsible for stopping the script executing on the Tentacle
                    lastStatusResponse = await Cancel(lastStatusResponse, CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    try
                    {
                        lastStatusResponse = await GetStatus(lastStatusResponse, scriptExecutionCancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        if (scriptExecutionCancellationToken.IsCancellationRequested) continue; // Enter cancellation mode.
                        throw;
                    }
                }

                OnScriptStatusResponseReceived(lastStatusResponse);

                if (GetState(lastStatusResponse) == ProcessState.Complete)
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

        protected abstract TStartCommand Map(StartScriptCommandV3Alpha command);

        protected abstract ScriptExecutionStatus MapToStatus(TScriptStatusResponse response);
        protected abstract ScriptExecutionResult MapToResult(TScriptStatusResponse response);

        protected abstract ProcessState GetState(TScriptStatusResponse response);

        protected abstract Task<TScriptStatusResponse> StartScript(TStartCommand command, CancellationToken scriptExecutionCancellationToken);

        protected abstract Task<TScriptStatusResponse> GetStatus(TScriptStatusResponse lastStatusResponse, CancellationToken cancellationToken);

        protected abstract Task<TScriptStatusResponse> Cancel(TScriptStatusResponse lastStatusResponse, CancellationToken cancellationToken);

        protected abstract Task<TScriptStatusResponse> Finish(TScriptStatusResponse lastStatusResponse, CancellationToken scriptExecutionCancellationToken);

        protected void OnScriptStatusResponseReceived(TScriptStatusResponse scriptStatusResponse)
        {
            onScriptStatusResponseReceived(MapToStatus(scriptStatusResponse));
        }
    }
}