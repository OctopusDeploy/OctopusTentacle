using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.SyncAndAsyncProxies
{
    internal class ClientCapabilitiesServiceV2SyncToAsyncProxy : IAsyncClientCapabilitiesServiceV2
    {
        private readonly IClientCapabilitiesServiceV2 service;

        public ClientCapabilitiesServiceV2SyncToAsyncProxy(IClientCapabilitiesServiceV2 service)
        {
            this.service = service;
        }

        public Task<CapabilitiesResponseV2> GetCapabilitiesAsync(HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            var result = service.GetCapabilities(halibutProxyRequestOptions);

            return Task.FromResult(result);
        }
    }
}