using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1Alpha;

namespace Octopus.Tentacle.Contracts.ClientServices
{
    public interface IAsyncClientKubernetesScriptServiceV1Alpha
    {
        Task<KubernetesScriptStatusResponseV1Alpha> StartScriptAsync(StartKubernetesScriptCommandV1Alpha command, HalibutProxyRequestOptions proxyRequestOptions);
        Task<KubernetesScriptStatusResponseV1Alpha> GetStatusAsync(KubernetesScriptStatusRequestV1Alpha request, HalibutProxyRequestOptions proxyRequestOptions);
        Task<KubernetesScriptStatusResponseV1Alpha> CancelScriptAsync(CancelKubernetesScriptCommandV1Alpha command, HalibutProxyRequestOptions proxyRequestOptions);
        Task CompleteScriptAsync(CompleteKubernetesScriptCommandV1Alpha command, HalibutProxyRequestOptions proxyRequestOptions);
    }
}