using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.SyncAndAsyncProxies
{
    internal class ClientScriptServiceV2AsyncToSyncProxy : IClientScriptServiceV2
    {
        private readonly IAsyncClientScriptServiceV2 service;

        public ClientScriptServiceV2AsyncToSyncProxy(IAsyncClientScriptServiceV2 service)
        {
            this.service = service;
        }

        public ScriptStatusResponseV2 CancelScript(CancelScriptCommandV2 command, HalibutProxyRequestOptions proxyRequestOptions)
        {
            return service.CancelScriptAsync(command, proxyRequestOptions).GetAwaiter().GetResult();
        }

        public void CompleteScript(CompleteScriptCommandV2 command, HalibutProxyRequestOptions proxyRequestOptions)
        {
            service.CompleteScriptAsync(command, proxyRequestOptions).GetAwaiter().GetResult();
        }

        public ScriptStatusResponseV2 GetStatus(ScriptStatusRequestV2 request, HalibutProxyRequestOptions proxyRequestOptions)
        {
            return service.GetStatusAsync(request, proxyRequestOptions).GetAwaiter().GetResult();
        }

        public ScriptStatusResponseV2 StartScript(StartScriptCommandV2 command, HalibutProxyRequestOptions proxyRequestOptions)
        {
            return service.StartScriptAsync(command, proxyRequestOptions).GetAwaiter().GetResult();
        }
    }
}