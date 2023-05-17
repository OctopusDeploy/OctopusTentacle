using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;
using Serilog;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders
{
    public class IsTentacleWaitingPendingRequestQueueDecoratorFactory : IPendingRequestQueueFactory
    {
        private Func<Uri, IPendingRequestQueue> createPendingRequestQueue;

        private volatile int TentaclesWaitingToDequeue = 0;

        public IsTentacleWaitingPendingRequestQueueDecoratorFactory() : this(uri => new PendingRequestQueue(new LogFactory().ForEndpoint(uri)))
        {
        }

        private ILogger logger;
        public IsTentacleWaitingPendingRequestQueueDecoratorFactory(Func<Uri, IPendingRequestQueue> createPendingRequestQueue)
        {
            this.createPendingRequestQueue = createPendingRequestQueue;
            this.logger = new SerilogLoggerBuilder().Build().ForContext<IsTentacleWaitingPendingRequestQueueDecoratorFactory>();
        }

        public IPendingRequestQueue CreateQueue(Uri endpoint)
        {
            var inner = createPendingRequestQueue(endpoint);
            return new DequeueActionPendingRequestQueue(inner, MarkTentacleWaitingForDequeue, MarkTentacleFinishedDequeue);
        }

        private void MarkTentacleWaitingForDequeue()
        {
            int tentaclesWaiting = Interlocked.Increment(ref TentaclesWaitingToDequeue);
            logger.Information("Tentacles waiting to dequeue is now {TentaclesWaitingToDequeue} (was incremented)", tentaclesWaiting); 
        }
        
        private void MarkTentacleFinishedDequeue()
        {
            int tentaclesWaiting = Interlocked.Decrement(ref TentaclesWaitingToDequeue);
            logger.Information("Tentacles waiting to dequeue is now {TentaclesWaitingToDequeue} (was decremented)", tentaclesWaiting);
        }
        
        public async Task WaitUntilATentacleIsWaitingToDequeueAMessage(CancellationToken cancellationToken)
        {
            logger.Information("Waiting for a tentacle to be waiting to dequeue");
            while (!cancellationToken.IsCancellationRequested)
            {
                if (TentaclesWaitingToDequeue > 0)
                {
                    logger.Information("Done Waiting for a tentacle to be waiting to dequeue, as a tentacle is waiting to dequeue something");
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(10));
            }
        }
    }

    class DequeueActionPendingRequestQueue : IPendingRequestQueue
    {
        private IPendingRequestQueue pendingRequestQueue;
        private Action beforeDequeue;
        private Action afterDequeue;

        public DequeueActionPendingRequestQueue(IPendingRequestQueue pendingRequestQueue, Action beforeDequeue, Action afterDequeue)
        {
            this.pendingRequestQueue = pendingRequestQueue;
            this.beforeDequeue = beforeDequeue;
            this.afterDequeue = afterDequeue;
        }

        public void ApplyResponse(ResponseMessage response, ServiceEndPoint destination)
        {
            pendingRequestQueue.ApplyResponse(response, destination);
        }

        public RequestMessage Dequeue()
        {
            beforeDequeue();
            try
            {
                return pendingRequestQueue.Dequeue();
            }
            finally
            {
                afterDequeue();
            }
        }

        public async Task<RequestMessage> DequeueAsync()
        {
            beforeDequeue();
            try
            {
                return await pendingRequestQueue.DequeueAsync();
            }
            finally
            {
                afterDequeue();
            }
        }

        public Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            return pendingRequestQueue.QueueAndWaitAsync(request, cancellationToken);
        }

        public bool IsEmpty => pendingRequestQueue.IsEmpty;
    }
}