using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.Capabilities;

namespace Octopus.Tentacle.Client.ClientServices
{
    public interface IClientCapabilitiesServiceV2
    {
        public CapabilitiesResponseV2 GetCapabilities(HalibutProxyRequestOptions halibutProxyRequestOptions);
    }
}