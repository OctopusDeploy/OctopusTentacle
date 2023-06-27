using Halibut.ServiceModel;
using Halibut.Transport.Caching;
using Octopus.Tentacle.Contracts.Capabilities;

namespace Octopus.Tentacle.Client.ClientServices
{
    public interface IClientCapabilitiesServiceV2
    {
        [CacheResponse(600)]
        public CapabilitiesResponseV2 GetCapabilities(HalibutProxyRequestOptions halibutProxyRequestOptions);
    }
}