using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;

namespace Octopus.Tentacle.Tests.Integration.Util.PendingRequestQueueHelpers
{
    public class CancellationTokenObservingPendingRequestQueueDecorator : IPendingRequestQueue
    {
        private IPendingRequestQueue pendingRequestQueue;

        public CancellationTokenObservingPendingRequestQueueDecorator(IPendingRequestQueue pendingRequestQueue)
        {
            this.pendingRequestQueue = pendingRequestQueue;
        }

        public void ApplyResponse(ResponseMessage response, ServiceEndPoint destination)
        {
            pendingRequestQueue.ApplyResponse(response, destination);
        }

        public RequestMessage Dequeue()
        {
            return pendingRequestQueue.Dequeue();
        }

        public Task<RequestMessage> DequeueAsync()
        {
            return pendingRequestQueue.DequeueAsync();
        }

        public Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return pendingRequestQueue.QueueAndWaitAsync(request, cancellationToken);
        }

        public bool IsEmpty => pendingRequestQueue.IsEmpty;
    }
}