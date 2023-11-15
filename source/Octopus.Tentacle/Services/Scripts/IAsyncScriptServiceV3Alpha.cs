using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Services.Scripts
{
    /// <remarks>
    /// This interface should mirror <see cref="IScriptServiceV3Alpha"/>.
    /// All return types must be <see cref="Task"/>/<see cref="Task{T}"/> and all methods must be suffixed with 'Async' with their last parameter must be a <see cref="CancellationToken"/>.
    /// </remarks>
    public interface IAsyncScriptServiceV3Alpha
    {
        Task<ScriptStatusResponseV3Alpha> StartScriptAsync(StartScriptCommandV3Alpha command, CancellationToken cancellationToken);
        Task<ScriptStatusResponseV3Alpha> GetStatusAsync(ScriptStatusRequestV3Alpha request, CancellationToken cancellationToken);
        Task<ScriptStatusResponseV3Alpha> CancelScriptAsync(CancelScriptCommandV3Alpha command, CancellationToken cancellationToken);
        Task CompleteScriptAsync(CompleteScriptCommandV3Alpha command, CancellationToken cancellationToken);
    }
}