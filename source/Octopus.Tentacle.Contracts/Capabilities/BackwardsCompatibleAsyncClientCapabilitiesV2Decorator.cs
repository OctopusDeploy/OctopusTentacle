using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Contracts.Capabilities
{
    public class BackwardsCompatibleAsyncClientCapabilitiesV2Decorator : IAsyncClientCapabilitiesServiceV2
    {
        private readonly IAsyncClientCapabilitiesServiceV2 inner;

        public BackwardsCompatibleAsyncClientCapabilitiesV2Decorator(IAsyncClientCapabilitiesServiceV2 inner)
        {
            this.inner = inner;
        }

        public async Task<CapabilitiesResponseV2> GetCapabilitiesAsync(HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            return await BackwardsCompatibleCapabilitiesV2Decorator.WithBackwardsCompatabilityAsync(async () => await inner.GetCapabilitiesAsync(halibutProxyRequestOptions));
        }
    }
}