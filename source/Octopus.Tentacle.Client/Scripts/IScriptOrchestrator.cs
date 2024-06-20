using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client.Scripts.Models;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.Scripts
{
    public interface IScriptOrchestrator
    {
        Task<ScriptExecutionResult> ExecuteScript(ExecuteScriptCommand command, CancellationToken scriptExecutionCancellationToken);
    }
    
    public interface IStructuredScriptOrchestrator<TScriptStatusResponse> {
        ScriptExecutionStatus MapToStatus(TScriptStatusResponse response);
        ScriptExecutionResult MapToResult(TScriptStatusResponse response);
        ProcessState GetState(TScriptStatusResponse response);
        Task<TScriptStatusResponse> StartScript(ExecuteScriptCommand command, CancellationToken scriptExecutionCancellationToken);
        Task<TScriptStatusResponse> GetStatus(TScriptStatusResponse lastStatusResponse, CancellationToken scriptExecutionCancellationToken);
        Task<TScriptStatusResponse> Cancel(TScriptStatusResponse lastStatusResponse, CancellationToken scriptExecutionCancellationToken);
        Task<TScriptStatusResponse> Finish(TScriptStatusResponse lastStatusResponse, CancellationToken scriptExecutionCancellationToken);
    }
}