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
        private readonly IPendingRequestQueue pendingRequestQueue;

        public CancellationTokenObservingPendingRequestQueueDecorator(IPendingRequestQueue pendingRequestQueue)
        {
            this.pendingRequestQueue = pendingRequestQueue;
        }

        public async Task ApplyResponse(ResponseMessage response, ServiceEndPoint destination)
        {
            await pendingRequestQueue.ApplyResponse(response, destination);
        }

        public async Task<RequestMessage> DequeueAsync(CancellationToken cancellationToken)
        {
            return await pendingRequestQueue.DequeueAsync(cancellationToken);
        }
        
        public Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("This method is obsolete, and should not be called in Tentacle");
        }

        public Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, RequestCancellationTokens requestCancellationTokens)
        {
            requestCancellationTokens.ConnectingCancellationToken.ThrowIfCancellationRequested();
            return pendingRequestQueue.QueueAndWaitAsync(request, requestCancellationTokens);
        }

        public bool IsEmpty => pendingRequestQueue.IsEmpty;
        public int Count => pendingRequestQueue.Count;
    }
}