using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Contracts.ClientServices
{
    public interface IAsyncClientScriptServiceV2
    {
        Task<ScriptStatusResponseV2> StartScriptAsync(StartScriptCommandV2 command, HalibutProxyRequestOptions proxyRequestOptions);
        Task<ScriptStatusResponseV2> GetStatusAsync(ScriptStatusRequestV2 request, HalibutProxyRequestOptions proxyRequestOptions);
        Task<ScriptStatusResponseV2> CancelScriptAsync(CancelScriptCommandV2 command, HalibutProxyRequestOptions proxyRequestOptions);
        Task CompleteScriptAsync(CompleteScriptCommandV2 command, HalibutProxyRequestOptions proxyRequestOptions);
    }
}