using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.ClientServices
{
    public interface IClientScriptService
    {
        ScriptTicket StartScript(StartScriptCommand command, HalibutProxyRequestOptions proxyRequestOptions);
        ScriptStatusResponse GetStatus(ScriptStatusRequest request, HalibutProxyRequestOptions proxyRequestOptions);
        ScriptStatusResponse CancelScript(CancelScriptCommand command, HalibutProxyRequestOptions proxyRequestOptions);
        ScriptStatusResponse CompleteScript(CompleteScriptCommand command, HalibutProxyRequestOptions proxyRequestOptions);
    }
}