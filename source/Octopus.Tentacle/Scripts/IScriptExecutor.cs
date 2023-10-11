using System.Threading;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Scripts
{
    public interface IScriptExecutor
    {
        IRunningScript ExecuteOnThread(StartScriptCommandV2 command, IScriptWorkspace workspace, ScriptStateStore? scriptStateStore, CancellationTokenSource cancellationTokenSource);
    }
}