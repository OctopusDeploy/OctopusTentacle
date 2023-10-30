using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Client.Decorators
{
    class HalibutExceptionAsyncCapabilitiesServiceV2Decorator : HalibutExceptionTentacleServiceDecorator, IAsyncClientCapabilitiesServiceV2
    {
        readonly IAsyncClientCapabilitiesServiceV2 inner;

        public HalibutExceptionAsyncCapabilitiesServiceV2Decorator(IAsyncClientCapabilitiesServiceV2 inner)
        {
            this.inner = inner;
        }

        public async Task<CapabilitiesResponseV2> GetCapabilitiesAsync(HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            return await HandleCancellationException(async () => await inner.GetCapabilitiesAsync(halibutProxyRequestOptions));
        }
    }
}