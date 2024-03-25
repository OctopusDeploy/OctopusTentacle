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

        GetCapabilitiesClientDecorator getCapabilitiesFunc = async (inner, options) => await inner.GetCapabilitiesAsync(options);
        Func<IAsyncClientCapabilitiesServiceV2, Task> beforeGetCapabilities = async _ => await Task.CompletedTask;
        Func<CapabilitiesResponseV2?, Task> afterGetCapabilities = async _ => await Task.CompletedTask;

        public CapabilitiesServiceV2DecoratorBuilder BeforeGetCapabilities(Func<Task> beforeGetCapabilities)
        {
            return BeforeGetCapabilities(async inner => await beforeGetCapabilities());
        }

        public CapabilitiesServiceV2DecoratorBuilder BeforeGetCapabilities(Func<IAsyncClientCapabilitiesServiceV2, Task> beforeGetCapabilities)
        {
            this.beforeGetCapabilities = beforeGetCapabilities;
            return this;
        }

        public CapabilitiesServiceV2DecoratorBuilder AfterGetCapabilities(Func<CapabilitiesResponseV2?, Task> afterGetCapabilities)
        {
            this.afterGetCapabilities = afterGetCapabilities;
            return this;
        }

        public CapabilitiesServiceV2DecoratorBuilder DecorateGetCapabilitiesWith(GetCapabilitiesClientDecorator getCapabilitiesFunc)
        {
            this.getCapabilitiesFunc = getCapabilitiesFunc;
            return this;
        }

        public Decorator<IAsyncClientCapabilitiesServiceV2> Build()
        {
            return inner => new FuncCapabilitiesServiceV2Decorator(
                inner,
                getCapabilitiesFunc,
                beforeGetCapabilities,
                afterGetCapabilities
            );
        }

        class FuncCapabilitiesServiceV2Decorator : IAsyncClientCapabilitiesServiceV2
        {
            readonly IAsyncClientCapabilitiesServiceV2 inner;
            readonly GetCapabilitiesClientDecorator getCapabilitiesFunc;
            readonly Func<IAsyncClientCapabilitiesServiceV2, Task> beforeGetCapabilities;
            readonly Func<CapabilitiesResponseV2?, Task> afterGetCapabilities;

            public FuncCapabilitiesServiceV2Decorator(
                IAsyncClientCapabilitiesServiceV2 inner,
                GetCapabilitiesClientDecorator getCapabilitiesFunc,
                Func<IAsyncClientCapabilitiesServiceV2, Task> beforeGetCapabilities,
                Func<CapabilitiesResponseV2?, Task> afterGetCapabilities
            )
            {
                this.inner = inner;
                this.getCapabilitiesFunc = getCapabilitiesFunc;
                this.beforeGetCapabilities = beforeGetCapabilities;
                this.afterGetCapabilities = afterGetCapabilities;
            }

            public async Task<CapabilitiesResponseV2> GetCapabilitiesAsync(HalibutProxyRequestOptions options)
            {
                CapabilitiesResponseV2? response = null;
                try
                {
                    await beforeGetCapabilities(inner);
                    response = await getCapabilitiesFunc(inner, options);
                    return response;
                }
                finally
                {
                    await afterGetCapabilities(response);
                }
            }
        }
    }
}