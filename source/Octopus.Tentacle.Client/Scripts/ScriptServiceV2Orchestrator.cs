using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client.Scripts.Execution;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Client.Scripts
{
    class ScriptServiceV2Orchestrator : ObservingScriptOrchestrator
    {
        readonly IScriptServiceExecutor scriptServiceExecutor;

        public ScriptServiceV2Orchestrator(
            IScriptServiceExecutor scriptServiceExecutor,
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            TentacleClientOptions clientOptions)
            : base(scriptObserverBackOffStrategy,
                onScriptStatusResponseReceived,
                onScriptCompleted,
                clientOptions)
        {
            this.scriptServiceExecutor = scriptServiceExecutor;
        }
        
        protected override async Task<ScriptStatusResponseV3Alpha> StartScript(StartScriptCommandV3Alpha command, CancellationToken scriptExecutionCancellationToken)
        {
            try
            {
                var scriptStatusResponse = await scriptServiceExecutor.StartScript(command, scriptExecutionCancellationToken).ConfigureAwait(false);
                return scriptStatusResponse;
            }
            catch (ScriptExecutionCancelledAfterPotentiallyStartingException)
            {
                // We have to assume the script started executing and call CancelScript and CompleteScript
                // We don't have a response so we need to create one to continue the execution flow
                var scriptStatusResponse = new ScriptStatusResponseV3Alpha(
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
        }

        protected override async Task<ScriptStatusResponseV3Alpha> GetStatus(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            try
            {
                return await scriptServiceExecutor.GetStatus(lastStatusResponse, scriptExecutionCancellationToken);
            }
            catch (Exception e) when (e is OperationCanceledException && scriptExecutionCancellationToken.IsCancellationRequested)
            {
                // Return the last known response without logs when cancellation occurs and let the script execution go into the CancelScript and CompleteScript flow
                return new ScriptStatusResponseV3Alpha(lastStatusResponse.ScriptTicket, lastStatusResponse.State, lastStatusResponse.ExitCode, new List<ProcessOutput>(), lastStatusResponse.NextLogSequence);
            }
        }
        
        protected override async Task<ScriptStatusResponseV3Alpha> Cancel(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            return await scriptServiceExecutor.Cancel(lastStatusResponse, scriptExecutionCancellationToken);
        }

        protected override async Task<ScriptStatusResponseV3Alpha> Finish(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken)
        {
            return await scriptServiceExecutor.Finish(lastStatusResponse, scriptExecutionCancellationToken);
        }
    }
}