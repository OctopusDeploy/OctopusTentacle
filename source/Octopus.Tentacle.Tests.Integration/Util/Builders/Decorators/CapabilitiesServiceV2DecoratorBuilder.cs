using System;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.Capabilities;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class CapabilitiesServiceV2DecoratorBuilder
    {
        public delegate CapabilitiesResponseV2 GetCapabilitiesClientDecorator(IClientCapabilitiesServiceV2 inner, HalibutProxyRequestOptions halibutProxyRequestOptions);

        private GetCapabilitiesClientDecorator getCapabilitiesFunc = (inner, options) => inner.GetCapabilities(options);

        public CapabilitiesServiceV2DecoratorBuilder BeforeGetCapabilities(Action beforeGetCapabilities)
        {
            return BeforeGetCapabilities(inner => beforeGetCapabilities());
        }

        public CapabilitiesServiceV2DecoratorBuilder BeforeGetCapabilities(Action<IClientCapabilitiesServiceV2> beforeGetCapabilities)
        {
            return DecorateGetCapabilitiesWith((inner, options) =>
            {
                beforeGetCapabilities(inner);
                return inner.GetCapabilities(options);
            });
        }

        public CapabilitiesServiceV2DecoratorBuilder DecorateGetCapabilitiesWith(GetCapabilitiesClientDecorator getCapabilitiesFunc)
        {
            this.getCapabilitiesFunc = getCapabilitiesFunc;
            return this;
        }

        public Func<IClientCapabilitiesServiceV2, IClientCapabilitiesServiceV2> Build()
        {
            return inner => new FuncCapabilitiesServiceV2Decorator(inner, getCapabilitiesFunc);
        }

        private class FuncCapabilitiesServiceV2Decorator : IClientCapabilitiesServiceV2
        {
            private readonly IClientCapabilitiesServiceV2 inner;
            private readonly GetCapabilitiesClientDecorator getCapabilitiesFunc;

            public FuncCapabilitiesServiceV2Decorator(IClientCapabilitiesServiceV2 inner, GetCapabilitiesClientDecorator getCapabilitiesFunc)
            {
                this.inner = inner;
                this.getCapabilitiesFunc = getCapabilitiesFunc;
            }

            public CapabilitiesResponseV2 GetCapabilities(HalibutProxyRequestOptions options)
            {
                return getCapabilitiesFunc(inner, options);
            }
        }
    }
}