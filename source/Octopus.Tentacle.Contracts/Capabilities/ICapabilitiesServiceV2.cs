using Halibut.Transport.Caching;

namespace Octopus.Tentacle.Contracts.Capabilities
{
    public interface ICapabilitiesServiceV2
    {
        [CacheResponse(600)]
        public CapabilitiesResponseV2 GetCapabilities();
    }
}