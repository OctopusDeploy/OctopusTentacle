using System;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Octopus.Tentacle.Tests.Integration.Support;

namespace Octopus.Tentacle.Tests.Integration.Util.PendingRequestQueueHelpers
{
    public class CancellationObservingPendingRequestQueueFactory : IPendingRequestQueueFactory
    {
        private readonly SyncOrAsyncHalibut syncOrAsyncHalibut;

        public CancellationObservingPendingRequestQueueFactory(SyncOrAsyncHalibut syncOrAsyncHalibut)
        {
            this.syncOrAsyncHalibut = syncOrAsyncHalibut;
        }

        public IPendingRequestQueue CreateQueue(Uri endpoint)
        {
            if (syncOrAsyncHalibut == SyncOrAsyncHalibut.Sync)
            {
#pragma warning disable CS0612
                return new CancellationTokenObservingPendingRequestQueueDecorator(new PendingRequestQueue(new LogFactory().ForEndpoint(endpoint)));
#pragma warning restore CS0612
            }

            return new CancellationTokenObservingPendingRequestQueueDecorator(new PendingRequestQueueAsync(new HalibutTimeoutsAndLimits(), new LogFactory().ForEndpoint(endpoint)));
        }
    }
}