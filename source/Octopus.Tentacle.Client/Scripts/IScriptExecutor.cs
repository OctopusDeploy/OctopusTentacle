using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client.EventDriven;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts
{
    public interface IScriptExecutor 
    {
        Task<(ScriptStatus, ICommandContext)> StartScript(ExecuteScriptCommand command,
            StartScriptIsBeingReAttempted startScriptIsBeingReAttempted,
            CancellationToken scriptExecutionCancellationToken);
        
        Task<(ScriptStatus, ICommandContext)> GetStatus(ICommandContext commandContext, CancellationToken scriptExecutionCancellationToken);
        
        Task<(ScriptStatus, ICommandContext)> CancelScript(ICommandContext commandContext, CancellationToken scriptExecutionCancellationToken);
        
        Task<ScriptStatus?> CleanUpScript(ICommandContext commandContext, CancellationToken scriptExecutionCancellationToken);
    }
}