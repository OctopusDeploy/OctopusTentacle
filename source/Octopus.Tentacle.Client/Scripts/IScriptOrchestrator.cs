using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Client.Scripts
{
    interface IScriptOrchestrator
    {
        Task<ScriptExecutionResult> ExecuteScript(StartScriptCommandV2 startScriptCommand, CancellationToken scriptExecutionCancellationToken);
    }
}