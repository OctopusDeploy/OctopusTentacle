using System;
using Octopus.Tentacle.Contracts.Capabilities;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class CapabilitiesServiceV2DecoratorBuilder
    {
        private Func<ICapabilitiesServiceV2, CapabilitiesResponseV2> getCapabilitiesFunc = inner => inner.GetCapabilities();

        public CapabilitiesServiceV2DecoratorBuilder BeforeGetCapabilities(Action beforeGetCapabilities)
        {
            return DecorateGetCapabilitiesWith((inner) =>
            {
                beforeGetCapabilities();
                return inner.GetCapabilities();
            });
        }

        public CapabilitiesServiceV2DecoratorBuilder DecorateGetCapabilitiesWith(Func<ICapabilitiesServiceV2, CapabilitiesResponseV2> getCapabilitiesFunc)
        {
            this.getCapabilitiesFunc = getCapabilitiesFunc;
            return this;
        }

        public Func<ICapabilitiesServiceV2, ICapabilitiesServiceV2> Build()
        {
            return inner => new FuncCapabilitiesServiceV2Decorator(inner, getCapabilitiesFunc);
        }

        private class FuncCapabilitiesServiceV2Decorator : ICapabilitiesServiceV2
        {
            private readonly ICapabilitiesServiceV2 inner;
            private readonly Func<ICapabilitiesServiceV2, CapabilitiesResponseV2> getCapabilitiesFunc;

            public FuncCapabilitiesServiceV2Decorator(ICapabilitiesServiceV2 inner, Func<ICapabilitiesServiceV2, CapabilitiesResponseV2> getCapabilitiesFunc)
            {
                this.inner = inner;
                this.getCapabilitiesFunc = getCapabilitiesFunc;
            }

            public CapabilitiesResponseV2 GetCapabilities()
            {
                return getCapabilitiesFunc(inner);
            }
        }
    }
}