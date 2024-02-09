using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Client.Scripts
{
    abstract class ObservingScriptOrchestrator : IScriptOrchestrator
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
            var scriptStatusResponse = await StartScript(startScriptCommand, scriptExecutionCancellationToken).ConfigureAwait(false);

            scriptStatusResponse = await ObserveUntilCompleteThenFinish(scriptStatusResponse, scriptExecutionCancellationToken).ConfigureAwait(false);

            if (scriptExecutionCancellationToken.IsCancellationRequested)
            {
                // Throw an error so the caller knows that execution of the script was cancelled
                throw new OperationCanceledException("Script execution was cancelled");
            }

            var mappedResponse = new ScriptExecutionResult(scriptStatusResponse.State, scriptStatusResponse.ExitCode);
            return mappedResponse;
        }
        
        protected async Task<ScriptStatusResponseV3Alpha> ObserveUntilCompleteThenFinish(
            ScriptStatusResponseV3Alpha scriptStatusResponse,
            CancellationToken scriptExecutionCancellationToken)
        {
            OnScriptStatusResponseReceived(scriptStatusResponse);

            var lastScriptStatus = await ObserveUntilComplete(scriptStatusResponse, scriptExecutionCancellationToken).ConfigureAwait(false);

            await onScriptCompleted(scriptExecutionCancellationToken).ConfigureAwait(false);

            lastScriptStatus = await Finish(lastScriptStatus, scriptExecutionCancellationToken).ConfigureAwait(false);

            return lastScriptStatus;
        }

        async Task<ScriptStatusResponseV3Alpha> ObserveUntilComplete(
            ScriptStatusResponseV3Alpha scriptStatusResponse,
            CancellationToken scriptExecutionCancellationToken)
        {
            var lastStatusResponse = scriptStatusResponse;
            var iteration = 0;
            var cancellationIteration = 0;

            while (lastStatusResponse.State != ProcessState.Complete)
            {
                if (scriptExecutionCancellationToken.IsCancellationRequested)
                {
                    lastStatusResponse = await Cancel(lastStatusResponse, scriptExecutionCancellationToken).ConfigureAwait(false);
                }
                else
                {
                    try
                    {
                        lastStatusResponse = await GetStatus(lastStatusResponse, scriptExecutionCancellationToken).ConfigureAwait(false);
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
        
        protected abstract Task<ScriptStatusResponseV3Alpha> StartScript(StartScriptCommandV3Alpha command, CancellationToken scriptExecutionCancellationToken);

        protected abstract Task<ScriptStatusResponseV3Alpha> GetStatus(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken);

        protected abstract Task<ScriptStatusResponseV3Alpha> Cancel(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken);

        protected abstract Task<ScriptStatusResponseV3Alpha> Finish(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken);

        protected void OnScriptStatusResponseReceived(ScriptStatusResponseV3Alpha scriptStatusResponse)
        {
            onScriptStatusResponseReceived(new(scriptStatusResponse.Logs));
        }
    }
}