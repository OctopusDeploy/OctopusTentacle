using System;
using Halibut.Diagnostics;
using Halibut.ServiceModel;

namespace Octopus.Tentacle.Tests.Integration.Util.PendingRequestQueueHelpers
{
    public class CancellationObservingPendingRequestQueueFactory : IPendingRequestQueueFactory
    {

        public IPendingRequestQueue CreateQueue(Uri endpoint)
        {
            return new CancellationTokenObservingPendingRequestQueueDecorator(new PendingRequestQueue(new LogFactory().ForEndpoint(endpoint)));
        }
    }
}