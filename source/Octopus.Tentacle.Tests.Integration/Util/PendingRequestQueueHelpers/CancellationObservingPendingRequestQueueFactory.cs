using System;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Octopus.Tentacle.Tests.Integration.Support;

namespace Octopus.Tentacle.Tests.Integration.Util.PendingRequestQueueHelpers
{
    public class CancellationObservingPendingRequestQueueFactory : IPendingRequestQueueFactory
    {
        public IPendingRequestQueue CreateQueue(Uri endpoint)
        {
            return new CancellationTokenObservingPendingRequestQueueDecorator(new PendingRequestQueueAsync(new HalibutTimeoutsAndLimitsForTestBuilder().Build(), new LogFactory().ForEndpoint(endpoint)));
        }
    }
}