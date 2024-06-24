using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts
{
    public interface IStructuredScriptExecutor {
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
        Task<(ScriptStatus, ICommandContext)> Cancel(ICommandContext commandContext, CancellationToken scriptExecutionCancellationToken);
        Task<ScriptStatus?> Finish(ICommandContext commandContext, CancellationToken scriptExecutionCancellationToken);
    }
}