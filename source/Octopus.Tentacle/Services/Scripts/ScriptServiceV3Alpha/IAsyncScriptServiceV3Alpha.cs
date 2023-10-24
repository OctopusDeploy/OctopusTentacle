using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Services.Scripts.ScriptServiceV3Alpha
{
    public interface IAsyncScriptServiceV3Alpha
    {
        Task<ScriptStatusResponseV3Alpha> StartScriptAsync(StartScriptCommandV3Alpha command, CancellationToken cancellationToken);
        Task<ScriptStatusResponseV3Alpha> GetStatusAsync(ScriptStatusRequestV3Alpha request, CancellationToken cancellationToken);
        Task<ScriptStatusResponseV3Alpha> CancelScriptAsync(CancelScriptCommandV3Alpha command, CancellationToken cancellationToken);
        Task CompleteScriptAsync(CompleteScriptCommandV3Alpha command, CancellationToken cancellationToken);
    }
}