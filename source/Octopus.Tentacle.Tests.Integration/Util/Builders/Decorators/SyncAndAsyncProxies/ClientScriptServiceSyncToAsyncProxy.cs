using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.SyncAndAsyncProxies
{
    internal class ClientScriptServiceSyncToAsyncProxy : IAsyncClientScriptService
    {
        private readonly IClientScriptService service;

        public ClientScriptServiceSyncToAsyncProxy(IClientScriptService service)
        {
            this.service = service;
        }

        public Task<ScriptTicket> StartScriptAsync(StartScriptCommand command, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            var result = service.StartScript(command, halibutProxyRequestOptions);

            return Task.FromResult(result);
        }

        public Task<ScriptStatusResponse> GetStatusAsync(ScriptStatusRequest request, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            var result = service.GetStatus(request, halibutProxyRequestOptions);

            return Task.FromResult(result);
        }

        public Task<ScriptStatusResponse> CancelScriptAsync(CancelScriptCommand command, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            var result = service.CancelScript(command, halibutProxyRequestOptions);

            return Task.FromResult(result);
        }

        public Task<ScriptStatusResponse> CompleteScriptAsync(CompleteScriptCommand command, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            var result = service.CompleteScript(command, halibutProxyRequestOptions);

            return Task.FromResult(result);
        }
    }
}