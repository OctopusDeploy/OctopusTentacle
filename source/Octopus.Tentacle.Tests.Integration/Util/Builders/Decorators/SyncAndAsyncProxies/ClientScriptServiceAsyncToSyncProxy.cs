using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.SyncAndAsyncProxies
{
    internal class ClientScriptServiceAsyncToSyncProxy : IClientScriptService
    {
        private readonly IAsyncClientScriptService service;

        public ClientScriptServiceAsyncToSyncProxy(IAsyncClientScriptService service)
        {
            this.service = service;
        }

        public ScriptStatusResponse CancelScript(CancelScriptCommand command, HalibutProxyRequestOptions proxyRequestOptions)
        {
            return service.CancelScriptAsync(command, proxyRequestOptions).GetAwaiter().GetResult();
        }

        public ScriptStatusResponse CompleteScript(CompleteScriptCommand command, HalibutProxyRequestOptions proxyRequestOptions)
        {
            return service.CompleteScriptAsync(command, proxyRequestOptions).GetAwaiter().GetResult();
        }

        public ScriptStatusResponse GetStatus(ScriptStatusRequest request, HalibutProxyRequestOptions proxyRequestOptions)
        {
            return service.GetStatusAsync(request, proxyRequestOptions).GetAwaiter().GetResult();
        }

        public ScriptTicket StartScript(StartScriptCommand command, HalibutProxyRequestOptions proxyRequestOptions)
        {
            return service.StartScriptAsync(command, proxyRequestOptions).GetAwaiter().GetResult();
        }
    }
}