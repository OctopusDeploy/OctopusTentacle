using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts
{
    public interface IScriptExecutor {
        Task<(ScriptStatus, ICommandContext)> StartScript(ExecuteScriptCommand command,
            StartScriptIsBeingReAttempted startScriptIsBeingReAttempted,
            CancellationToken scriptExecutionCancellationToken);
        
        /// <summary>
        /// Returns a status or null when scriptExecutionCancellationToken is null. 
        /// </summary>
        /// <param name="commandContext"></param>
        /// <param name="scriptExecutionCancellationToken"></param>
        /// <returns></returns>
        Task<(ScriptStatus, ICommandContext)> GetStatus(ICommandContext commandContext, CancellationToken scriptExecutionCancellationToken);
        
        Task<(ScriptStatus, ICommandContext)> CancelScript(ICommandContext commandContext, CancellationToken scriptExecutionCancellationToken);
        
        /// <summary>
        /// Use this cancel method if only the ScriptTicket is known, e.g. we called StartScript but never got a response.
        /// Inheritors  
        /// </summary>
        /// <param name="scriptTicket"></param>
        /// <param name="hasStartScriptBeenCalledBefore"></param>
        /// <returns></returns>
        // Task<(ScriptStatus, ICommandContext)> CancelScript(ScriptTicket scriptTicket, CancellationToken cancellationToken);
        Task<ScriptStatus?> CleanUpScript(ICommandContext commandContext, CancellationToken scriptExecutionCancellationToken);
    }
}