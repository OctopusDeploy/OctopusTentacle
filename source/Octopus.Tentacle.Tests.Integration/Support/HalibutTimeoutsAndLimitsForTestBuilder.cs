using Halibut.Diagnostics;

namespace Octopus.Tentacle.Tests.Integration.Support
{
    public class HalibutTimeoutsAndLimitsForTestBuilder
    {

        public HalibutTimeoutsAndLimits Build()
        {
            var halibutTimeoutAndLimits = HalibutTimeoutsAndLimits.RecommendedValues();
            // Lets dogfood this in our tests.
            halibutTimeoutAndLimits.TcpNoDelay = true;
            halibutTimeoutAndLimits.UseAsyncListener = true;
            return halibutTimeoutAndLimits;
        }
    }
}