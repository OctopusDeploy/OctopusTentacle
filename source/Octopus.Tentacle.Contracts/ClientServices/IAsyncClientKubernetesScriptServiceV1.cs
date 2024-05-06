using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1;

namespace Octopus.Tentacle.Contracts.ClientServices
{
    public interface IAsyncClientKubernetesScriptServiceV1
    {
        Task<KubernetesScriptStatusResponseV1> StartScriptAsync(StartKubernetesScriptCommandV1 command, HalibutProxyRequestOptions proxyRequestOptions);
        Task<KubernetesScriptStatusResponseV1> GetStatusAsync(KubernetesScriptStatusRequestV1 request, HalibutProxyRequestOptions proxyRequestOptions);
        Task<KubernetesScriptStatusResponseV1> CancelScriptAsync(CancelKubernetesScriptCommandV1 command, HalibutProxyRequestOptions proxyRequestOptions);
        Task CompleteScriptAsync(CompleteKubernetesScriptCommandV1 command, HalibutProxyRequestOptions proxyRequestOptions);
    }
}