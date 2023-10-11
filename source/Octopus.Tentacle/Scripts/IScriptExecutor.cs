using System.Threading;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Scripts
{
    public interface IScriptExecutor
    {
        IRunningScript ExecuteOnBackgroundThread(StartScriptCommandV2 command, IScriptWorkspace workspace, ScriptStateStore? scriptStateStore, CancellationTokenSource cancellationTokenSource);
        IRunningScript Execute(ScriptTicket ticket, string serverTaskId, IScriptWorkspace workspace, CancellationTokenSource cancellationTokenSource);
    }
}