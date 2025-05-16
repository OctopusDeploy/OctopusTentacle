using System;
using System.Threading;
using System.Threading.Tasks;
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

        public Task<IPendingRequestQueue> CreateQueueAsync(Uri endpoint, CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateQueue(endpoint));
        }
    }
}