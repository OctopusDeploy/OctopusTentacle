using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts
{
    public interface IScriptOrchestrator
    {
        Task<ScriptExecutionResult> ExecuteScript(ExecuteScriptCommand command, CancellationToken scriptExecutionCancellationToken);
    }
    
    public interface IStructuredScriptOrchestrator {
        Task<StartScriptResult> StartScript(ExecuteScriptCommand command, CancellationToken scriptExecutionCancellationToken);
        /// <summary>
        /// Returns a status or null when scriptExecutionCancellationToken is null. 
        /// </summary>
        /// <param name="lastStatusResponse"></param>
        /// <param name="scriptExecutionCancellationToken"></param>
        /// <returns></returns>
        Task<(ScriptStatus, ITicketForNextStatus)> GetStatus(ITicketForNextStatus lastStatusResponse, CancellationToken scriptExecutionCancellationToken);
        Task<(ScriptStatus, ITicketForNextStatus)> Cancel(ITicketForNextStatus lastStatusResponse, CancellationToken scriptExecutionCancellationToken);
        Task<ScriptStatus?> Finish(ITicketForNextStatus lastStatusResponse, CancellationToken scriptExecutionCancellationToken);
    }


    public class StartScriptResult
    {
        public StartScriptResult(ScriptStatus status, ITicketForNextStatus ticketForNextStatus, bool scriptMayBeStarted)
        {
            Status = status;
            TicketForNextStatus = ticketForNextStatus;
            ScriptMayBeStarted = scriptMayBeStarted;
        }

        public ScriptStatus Status { get; }
        public ITicketForNextStatus TicketForNextStatus { get; }
        public bool ScriptMayBeStarted { get; }
    }
}