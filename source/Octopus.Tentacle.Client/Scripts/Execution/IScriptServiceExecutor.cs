using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Client.Scripts.Execution
{
    interface IScriptServiceExecutor
    {
        Task<ScriptStatusResponseV3Alpha> StartScript(StartScriptCommandV3Alpha command, CancellationToken scriptExecutionCancellationToken);
        Task<ScriptStatusResponseV3Alpha> GetStatus(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken);
        Task<ScriptStatusResponseV3Alpha> GetStatus(CommandContextV3Alpha commandContext, CancellationToken scriptExecutionCancellationToken);
        Task<ScriptStatusResponseV3Alpha> Cancel(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken);
        Task<ScriptStatusResponseV3Alpha> Cancel(CommandContextV3Alpha commandContext, CancellationToken scriptExecutionCancellationToken);
        Task<ScriptStatusResponseV3Alpha> Finish(ScriptStatusResponseV3Alpha lastStatusResponse, CancellationToken scriptExecutionCancellationToken);
        Task<ScriptStatusResponseV3Alpha> Finish(CommandContextV3Alpha commandContext, CancellationToken scriptExecutionCancellationToken);
    }
}