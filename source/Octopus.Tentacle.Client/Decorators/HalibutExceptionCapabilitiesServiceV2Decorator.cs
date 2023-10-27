using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Client.Decorators
{
    class HalibutExceptionCapabilitiesServiceV2Decorator : HalibutExceptionTentacleServiceDecorator, IClientCapabilitiesServiceV2
    {
        readonly IClientCapabilitiesServiceV2 inner;

        public HalibutExceptionCapabilitiesServiceV2Decorator(IClientCapabilitiesServiceV2 inner)
        {
            this.inner = inner;
        }

        public CapabilitiesResponseV2 GetCapabilities(HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            return HandleCancellationException(() => inner.GetCapabilities(halibutProxyRequestOptions));
        }
    }

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