using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;

namespace Octopus.Tentacle.Tests.Integration.Util.PendingRequestQueueHelpers
{
    public class CancellationTokenObservingPendingRequestQueueDecorator : IPendingRequestQueue
    {
        readonly IPendingRequestQueue pendingRequestQueue;

        public CancellationTokenObservingPendingRequestQueueDecorator(IPendingRequestQueue pendingRequestQueue)
        {
            this.pendingRequestQueue = pendingRequestQueue;
        }

        public async Task ApplyResponse(ResponseMessage response, ServiceEndPoint destination)
        {
            await pendingRequestQueue.ApplyResponse(response, destination);
        }

        public async Task<RequestMessageWithCancellationToken> DequeueAsync(CancellationToken cancellationToken)
        {
            return await pendingRequestQueue.DequeueAsync(cancellationToken);
        }

        public Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return pendingRequestQueue.QueueAndWaitAsync(request, cancellationToken);
        }

        public bool IsEmpty => pendingRequestQueue.IsEmpty;
        public int Count => pendingRequestQueue.Count;
    }
}