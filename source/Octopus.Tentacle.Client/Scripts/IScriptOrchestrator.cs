using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client.Scripts.Models;

namespace Octopus.Tentacle.Client.Scripts
{
    interface IScriptOrchestrator
    {
        Task<ScriptExecutionResult> ExecuteScript(ExecuteScriptCommand command, CancellationToken scriptExecutionCancellationToken);
    }
}