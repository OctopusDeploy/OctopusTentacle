using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1;

namespace Octopus.Tentacle.Services.Scripts.Kubernetes
{
    /// <remarks>
    /// This interface should mirror <see cref="IKubernetesScriptServiceV1"/>.
    /// All return types must be <see cref="Task"/>/<see cref="Task{T}"/> and all methods must be suffixed with 'Async' with their last parameter must be a <see cref="CancellationToken"/>.
    /// </remarks>
    public interface IAsyncKubernetesScriptServiceV1
    {
        Task<KubernetesScriptStatusResponseV1> StartScriptAsync(StartKubernetesScriptCommandV1 command, CancellationToken cancellationToken);
        Task<KubernetesScriptStatusResponseV1> GetStatusAsync(KubernetesScriptStatusRequestV1 request, CancellationToken cancellationToken);
        Task<KubernetesScriptStatusResponseV1> CancelScriptAsync(CancelKubernetesScriptCommandV1 command, CancellationToken cancellationToken);
        Task CompleteScriptAsync(CompleteKubernetesScriptCommandV1 command, CancellationToken cancellationToken);
    }
}