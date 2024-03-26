using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1Alpha;

namespace Octopus.Tentacle.Services.Scripts
{
    /// <remarks>
    /// This interface should mirror <see cref="IKubernetesScriptServiceV1Alpha"/>.
    /// All return types must be <see cref="Task"/>/<see cref="Task{T}"/> and all methods must be suffixed with 'Async' with their last parameter must be a <see cref="CancellationToken"/>.
    /// </remarks>
    public interface IAsyncScriptServiceV3Alpha
    {
        Task<KubernetesScriptStatusResponseV1Alpha> StartScriptAsync(StartKubernetesScriptCommandV1Alpha command, CancellationToken cancellationToken);
        Task<KubernetesScriptStatusResponseV1Alpha> GetStatusAsync(KubernetesScriptStatusRequestV1Alpha request, CancellationToken cancellationToken);
        Task<KubernetesScriptStatusResponseV1Alpha> CancelScriptAsync(CancelKubernetesScriptCommandV1Alpha command, CancellationToken cancellationToken);
        Task CompleteScriptAsync(CompleteKubernetesScriptCommandV1Alpha command, CancellationToken cancellationToken);
    }
}