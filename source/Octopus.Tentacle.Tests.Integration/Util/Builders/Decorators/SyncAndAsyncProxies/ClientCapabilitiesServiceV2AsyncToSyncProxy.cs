using System;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.SyncAndAsyncProxies
{
    internal class ClientCapabilitiesServiceV2AsyncToSyncProxy : IClientCapabilitiesServiceV2
    {
        private readonly IAsyncClientCapabilitiesServiceV2 service;

        public ClientCapabilitiesServiceV2AsyncToSyncProxy(IAsyncClientCapabilitiesServiceV2 service)
        {
            this.service = service;
        }

        public CapabilitiesResponseV2 GetCapabilities(HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            return service.GetCapabilitiesAsync(halibutProxyRequestOptions).GetAwaiter().GetResult();
        }
    }
}