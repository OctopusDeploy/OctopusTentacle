using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts
{
    public sealed class ObservingScriptOrchestrator : IScriptOrchestrator
    {
        readonly IScriptObserverBackoffStrategy scriptObserverBackOffStrategy;
        readonly OnScriptStatusResponseReceived onScriptStatusResponseReceived;
        readonly OnScriptCompleted onScriptCompleted;

        IStructuredScriptOrchestrator structuredScriptOrchestrator;

        public ObservingScriptOrchestrator(
            IScriptObserverBackoffStrategy scriptObserverBackOffStrategy,
            OnScriptStatusResponseReceived onScriptStatusResponseReceived,
            OnScriptCompleted onScriptCompleted,
            IStructuredScriptOrchestrator structuredScriptOrchestrator)
        {
            this.structuredScriptOrchestrator = structuredScriptOrchestrator;
            this.scriptObserverBackOffStrategy = scriptObserverBackOffStrategy;
            this.onScriptStatusResponseReceived = onScriptStatusResponseReceived;
            this.onScriptCompleted = onScriptCompleted;
        }

        public async Task<ScriptExecutionResult> ExecuteScript(ExecuteScriptCommand command, CancellationToken scriptExecutionCancellationToken)
        {

            var (scriptStatus, ticketForNextStatus) = await structuredScriptOrchestrator.StartScript(command, scriptExecutionCancellationToken).ConfigureAwait(false);

            (scriptStatus, ticketForNextStatus) = await ObserveUntilCompleteThenFinish(scriptStatus, ticketForNextStatus, scriptExecutionCancellationToken).ConfigureAwait(false);

            if (scriptExecutionCancellationToken.IsCancellationRequested)
            {
                // Throw an error so the caller knows that execution of the script was cancelled
                throw new OperationCanceledException("Script execution was cancelled");
            }

            
            return new ScriptExecutionResult(scriptStatus.State, scriptStatus.ExitCode!.Value);
        }

        async Task<(ScriptStatus, ITicketForNextStatus)> ObserveUntilCompleteThenFinish(
            ScriptStatus scriptStatus,
            ITicketForNextStatus ticketForNextStatus,
            CancellationToken scriptExecutionCancellationToken)
        {
            OnScriptStatusResponseReceived(scriptStatus);

            var (lastStatusResponse, lastTicketForNextStatus) =  await ObserveUntilComplete(scriptStatus, ticketForNextStatus, scriptExecutionCancellationToken).ConfigureAwait(false);

            await onScriptCompleted(scriptExecutionCancellationToken).ConfigureAwait(false);

            (lastStatusResponse, lastTicketForNextStatus)  = await structuredScriptOrchestrator.Finish(lastTicketForNextStatus, scriptExecutionCancellationToken).ConfigureAwait(false);
            
            OnScriptStatusResponseReceived(lastStatusResponse);

            return (lastStatusResponse, lastTicketForNextStatus);
        }

        async Task<(ScriptStatus lastStatusResponse, ITicketForNextStatus lastTicketForNextStatus)> ObserveUntilComplete(
            ScriptStatus scriptStatus,
            ITicketForNextStatus ticketForNextStatus,
            CancellationToken scriptExecutionCancellationToken)
        {
            var lastTicketForNextStatus = ticketForNextStatus;
            var lastStatusResponse = scriptStatus;
            var iteration = 0;
            var cancellationIteration = 0;

            while (lastStatusResponse.State != ProcessState.Complete)
            {
                if (scriptExecutionCancellationToken.IsCancellationRequested)
                {
                    (lastStatusResponse, lastTicketForNextStatus) = await structuredScriptOrchestrator.Cancel(lastTicketForNextStatus, scriptExecutionCancellationToken).ConfigureAwait(false);
                }
                else
                {
                    try
                    {
                        (lastStatusResponse, lastTicketForNextStatus) = await structuredScriptOrchestrator.GetStatus(lastTicketForNextStatus, scriptExecutionCancellationToken).ConfigureAwait(false);
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

            new ShortCutTakenHere();
            return (lastStatusResponse, lastTicketForNextStatus);
        }

        void OnScriptStatusResponseReceived(ScriptStatus scriptStatusResponse)
        {
            var scriptExecutionStatus = new ScriptExecutionStatus(scriptStatusResponse.Logs);
            onScriptStatusResponseReceived(scriptExecutionStatus);
        }
    }
}