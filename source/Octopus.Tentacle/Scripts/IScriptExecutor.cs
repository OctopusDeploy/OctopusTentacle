using System.Threading;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Scripts
{
    public interface IScriptExecutor
    {
        IRunningScript ExecuteOnBackgroundThread(StartScriptCommandV3Alpha startScriptCommand, IScriptWorkspace workspace, ScriptStateStore? scriptStateStore, CancellationToken cancellationToken);
        IRunningScript Execute(ScriptTicket ticket, string serverTaskId, IScriptWorkspace workspace, CancellationTokenSource cancellationTokenSource);
    }
}