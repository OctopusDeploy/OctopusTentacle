using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Client.Scripts
{
    interface IScriptOrchestrator
    {
        Task<ScriptExecutionResult> ExecuteScript(StartScriptCommandV3Alpha startScriptCommand, CancellationToken scriptExecutionCancellationToken);
    }
}