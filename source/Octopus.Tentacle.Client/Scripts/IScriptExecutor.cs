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
        Task<(ScriptStatus, CommandContext)> StartScript(ExecuteScriptCommand command,
            StartScriptIsBeingReAttempted startScriptIsBeingReAttempted,
            CancellationToken scriptExecutionCancellationToken);
        
        Task<(ScriptStatus, CommandContext)> GetStatus(CommandContext commandContext, CancellationToken scriptExecutionCancellationToken);
        
        Task<(ScriptStatus, CommandContext)> CancelScript(CommandContext commandContext, CancellationToken scriptExecutionCancellationToken);
        
        Task<ScriptStatus?> CompleteScript(CommandContext commandContext, CancellationToken scriptExecutionCancellationToken);
    }
}