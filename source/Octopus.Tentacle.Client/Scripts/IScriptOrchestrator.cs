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
    
    public interface IStructuredScriptOrchestrator<TStartCommand, TScriptStatusResponse> {
        TStartCommand Map(ExecuteScriptCommand command);
        ScriptExecutionStatus MapToStatus(TScriptStatusResponse response);
        ScriptExecutionResult MapToResult(TScriptStatusResponse response);
        ProcessState GetState(TScriptStatusResponse response);
        Task<TScriptStatusResponse> StartScript(TStartCommand command, CancellationToken scriptExecutionCancellationToken);
        Task<TScriptStatusResponse> GetStatus(TScriptStatusResponse lastStatusResponse, CancellationToken scriptExecutionCancellationToken);
        Task<TScriptStatusResponse> Cancel(TScriptStatusResponse lastStatusResponse, CancellationToken scriptExecutionCancellationToken);
        Task<TScriptStatusResponse> Finish(TScriptStatusResponse lastStatusResponse, CancellationToken scriptExecutionCancellationToken);
    }
}