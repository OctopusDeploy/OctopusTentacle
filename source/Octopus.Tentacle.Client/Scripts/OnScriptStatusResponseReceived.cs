using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Client.Scripts
{
    public delegate void OnScriptStatusResponseReceived(ScriptStatusResponseV2 response);
    public delegate Task OnScriptCompleted(CancellationToken cancellationToken);
}