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
        Task<ScriptExecutorResult> StartScript(ExecuteScriptCommand command,
            StartScriptIsBeingReAttempted startScriptIsBeingReAttempted,
            CancellationToken scriptExecutionCancellationToken);
        
        Task<ScriptExecutorResult> GetStatus(CommandContext commandContext, CancellationToken scriptExecutionCancellationToken);
        
        Task<ScriptExecutorResult> CancelScript(CommandContext commandContext);
        
        Task<ScriptStatus?> CompleteScript(CommandContext commandContext, CancellationToken scriptExecutionCancellationToken);
    }
}