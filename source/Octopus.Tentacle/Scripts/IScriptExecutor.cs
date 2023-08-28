using System.Threading;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Scripts
{
    public interface IScriptExecutor
    {
        IRunningScript Execute(ScriptTicket ticket, string serverTaskId, IScriptWorkspace workspace, CancellationTokenSource cancellationTokenSource);
    }
}