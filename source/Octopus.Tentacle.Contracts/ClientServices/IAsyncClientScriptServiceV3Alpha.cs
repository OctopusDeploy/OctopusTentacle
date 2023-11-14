using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;

namespace Octopus.Tentacle.Contracts.ClientServices
{
    public interface IAsyncClientScriptServiceV3Alpha
    {
        Task<ScriptStatusResponseV3Alpha> StartScriptAsync(StartScriptCommandV3Alpha command, HalibutProxyRequestOptions proxyRequestOptions);
        Task<ScriptStatusResponseV3Alpha> GetStatusAsync(ScriptStatusRequestV3Alpha request, HalibutProxyRequestOptions proxyRequestOptions);
        Task<ScriptStatusResponseV3Alpha> CancelScriptAsync(CancelScriptCommandV3Alpha command, HalibutProxyRequestOptions proxyRequestOptions);
        Task CompleteScriptAsync(CompleteScriptCommandV3Alpha command, HalibutProxyRequestOptions proxyRequestOptions);
    }
}