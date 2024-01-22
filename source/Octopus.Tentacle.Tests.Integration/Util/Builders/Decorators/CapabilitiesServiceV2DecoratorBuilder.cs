using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class CapabilitiesServiceV2DecoratorBuilder
    {
        public delegate Task<CapabilitiesResponseV2> GetCapabilitiesClientDecorator(IAsyncClientCapabilitiesServiceV2 inner, HalibutProxyRequestOptions halibutProxyRequestOptions);

        private GetCapabilitiesClientDecorator getCapabilitiesFunc = async (inner, options) => await inner.GetCapabilitiesAsync(options);

        public CapabilitiesServiceV2DecoratorBuilder BeforeGetCapabilities(Func<Task> beforeGetCapabilities)
        {
            return BeforeGetCapabilities(async inner => await beforeGetCapabilities());
        }

        public CapabilitiesServiceV2DecoratorBuilder BeforeGetCapabilities(Func<IAsyncClientCapabilitiesServiceV2, Task> beforeGetCapabilities)
        {
            return DecorateGetCapabilitiesWith(async (inner, options) =>
            {
                await beforeGetCapabilities(inner);
                return await inner.GetCapabilitiesAsync(options);
            });
        }

        public CapabilitiesServiceV2DecoratorBuilder AfterGetCapabilities(Func<CapabilitiesResponseV2, Task> afterGetCapabilities)
        {
            return DecorateGetCapabilitiesWith(async (inner, options) =>
            {
                var response = await inner.GetCapabilitiesAsync(options);
                await afterGetCapabilities(response);
                return response;
            });
        }

        public CapabilitiesServiceV2DecoratorBuilder DecorateGetCapabilitiesWith(GetCapabilitiesClientDecorator getCapabilitiesFunc)
        {
            this.getCapabilitiesFunc = getCapabilitiesFunc;
            return this;
        }

        public Decorator<IAsyncClientCapabilitiesServiceV2> Build()
        {
            return inner => new FuncCapabilitiesServiceV2Decorator(inner, getCapabilitiesFunc);
        }

        private class FuncCapabilitiesServiceV2Decorator : IAsyncClientCapabilitiesServiceV2
        {
            private readonly IAsyncClientCapabilitiesServiceV2 inner;
            private readonly GetCapabilitiesClientDecorator getCapabilitiesFunc;

            public FuncCapabilitiesServiceV2Decorator(IAsyncClientCapabilitiesServiceV2 inner, GetCapabilitiesClientDecorator getCapabilitiesFunc)
            {
                this.inner = inner;
                this.getCapabilitiesFunc = getCapabilitiesFunc;
            }

            public async Task<CapabilitiesResponseV2> GetCapabilitiesAsync(HalibutProxyRequestOptions options)
            {
                return await getCapabilitiesFunc(inner, options);
            }
        }
    }
}