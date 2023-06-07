using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;

namespace Octopus.Tentacle.Contracts.Capabilities
{
    public class BackwardsCompatibleClientCapabilitiesV2Decorator : IClientCapabilitiesServiceV2
    {
        private readonly IClientCapabilitiesServiceV2 inner;

        public BackwardsCompatibleClientCapabilitiesV2Decorator(IClientCapabilitiesServiceV2 inner)
        {
            this.inner = inner;
        }

        public CapabilitiesResponseV2 GetCapabilities(HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            return BackwardsCompatibleCapabilitiesV2Decorator.WithBackwardsCompatability(() => inner.GetCapabilities(halibutProxyRequestOptions));
        }
    }
}