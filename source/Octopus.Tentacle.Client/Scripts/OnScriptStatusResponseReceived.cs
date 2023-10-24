using System.Threading;
using System.Threading.Tasks;
namespace Octopus.Tentacle.Client.Scripts
{
    public delegate void OnScriptStatusResponseReceived(ScriptExecutionStatus status);
    public delegate Task OnScriptCompleted(CancellationToken cancellationToken);
}