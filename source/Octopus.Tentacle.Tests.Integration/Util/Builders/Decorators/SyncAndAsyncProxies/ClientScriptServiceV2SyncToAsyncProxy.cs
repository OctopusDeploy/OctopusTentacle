using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.ClientServices;
using Octopus.Tentacle.Contracts.ScriptServiceV2;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.SyncAndAsyncProxies
{
    internal class ClientScriptServiceV2SyncToAsyncProxy : IAsyncClientScriptServiceV2
    {
        private readonly IClientScriptServiceV2 service;

        public ClientScriptServiceV2SyncToAsyncProxy(IClientScriptServiceV2 service)
        {
            this.service = service;
        }

        public Task<ScriptStatusResponseV2> CancelScriptAsync(CancelScriptCommandV2 command, HalibutProxyRequestOptions proxyRequestOptions)
        {
            var result = service.CancelScript(command, proxyRequestOptions);

            return Task.FromResult(result);
        }

        public Task CompleteScriptAsync(CompleteScriptCommandV2 command, HalibutProxyRequestOptions proxyRequestOptions)
        {
            service.CompleteScript(command, proxyRequestOptions);

            return Task.CompletedTask;
        }

        public Task<ScriptStatusResponseV2> GetStatusAsync(ScriptStatusRequestV2 request, HalibutProxyRequestOptions proxyRequestOptions)
        {
            var result = service.GetStatus(request, proxyRequestOptions);

            return Task.FromResult(result);
        }

        public Task<ScriptStatusResponseV2> StartScriptAsync(StartScriptCommandV2 command, HalibutProxyRequestOptions proxyRequestOptions)
        {
            var result = service.StartScript(command, proxyRequestOptions);

            return Task.FromResult(result);
        }
    }
}