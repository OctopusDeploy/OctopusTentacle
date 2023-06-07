using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Client.ClientServices
{
    public interface IClientScriptServiceV2
    {
        ScriptStatusResponseV2 StartScript(StartScriptCommandV2 command, HalibutProxyRequestOptions proxyRequestOptions);
        ScriptStatusResponseV2 GetStatus(ScriptStatusRequestV2 request, HalibutProxyRequestOptions proxyRequestOptions);
        ScriptStatusResponseV2 CancelScript(CancelScriptCommandV2 command, HalibutProxyRequestOptions proxyRequestOptions);
        void CompleteScript(CompleteScriptCommandV2 command, HalibutProxyRequestOptions proxyRequestOptions);
    }
}