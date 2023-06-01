using System;
using System.Threading;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts.Capabilities;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators
{
    public class CapabilitiesServiceV2CallCounts
    {
        public long GetCapabilitiesCallCountStarted;

        public long GetCapabilitiesCallCountComplete;
    }

    public class CountingCallsCapabilitiesServiceV2Decorator : IClientCapabilitiesServiceV2
    {
        private readonly CapabilitiesServiceV2CallCounts counts;

        private readonly IClientCapabilitiesServiceV2 inner;

        public CountingCallsCapabilitiesServiceV2Decorator(IClientCapabilitiesServiceV2 inner, CapabilitiesServiceV2CallCounts counts)
        {
            this.inner = inner;
            this.counts = counts;
        }

        public CapabilitiesResponseV2 GetCapabilities(HalibutProxyRequestOptions options)
        {
            Interlocked.Increment(ref counts.GetCapabilitiesCallCountStarted);
            try
            {
                return inner.GetCapabilities(options);
            }
            finally
            {
                Interlocked.Increment(ref counts.GetCapabilitiesCallCountComplete);
            }
        }
    }

    public static class TentacleServiceDecoratorBuilderCountingCallsCapabilitiesServiceV2DecoratorExtensionMethods
    {
        public static TentacleServiceDecoratorBuilder CountCallsToCapabilitiesServiceV2(this TentacleServiceDecoratorBuilder tentacleServiceDecoratorBuilder, out CapabilitiesServiceV2CallCounts CapabilitiesServiceV2CallCounts)
        {
            var myCapabilitiesServiceV2CallCounts = new CapabilitiesServiceV2CallCounts();
            CapabilitiesServiceV2CallCounts = myCapabilitiesServiceV2CallCounts;
            return tentacleServiceDecoratorBuilder.DecorateCapabilitiesServiceV2With(inner => new CountingCallsCapabilitiesServiceV2Decorator(inner, myCapabilitiesServiceV2CallCounts));
        }
    }
}