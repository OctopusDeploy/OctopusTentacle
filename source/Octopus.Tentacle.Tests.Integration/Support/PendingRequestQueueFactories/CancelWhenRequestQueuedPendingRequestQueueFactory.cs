using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;

namespace Octopus.Tentacle.Tests.Integration.Support.PendingRequestQueueFactories
{
    /// <summary>
    /// CancelWhenRequestQueuedPendingRequestQueueFactory cancels the cancellation token source when a request is queued
    /// </summary>
    public class CancelWhenRequestQueuedPendingRequestQueueFactory : IPendingRequestQueueFactory
    {
        readonly CancellationTokenSource cancellationTokenSource;
        readonly HalibutTimeoutsAndLimits halibutTimeoutsAndLimits;
        readonly Func<Task<bool>> shouldCancel;

        public CancelWhenRequestQueuedPendingRequestQueueFactory(CancellationTokenSource cancellationTokenSource, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits,  Func<Task<bool>> shouldCancel)
        {
            this.cancellationTokenSource = cancellationTokenSource;
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
            this.shouldCancel = shouldCancel;
        }

        public IPendingRequestQueue CreateQueue(Uri endpoint)
        {
            return new Decorator(new PendingRequestQueueAsync(halibutTimeoutsAndLimits, new LogFactory().ForEndpoint(endpoint)), cancellationTokenSource, shouldCancel);
        }

        class Decorator : IPendingRequestQueue
        {
            readonly CancellationTokenSource cancellationTokenSource;
            readonly Func<Task<bool>> shouldCancel;
            readonly IPendingRequestQueue inner;

            public Decorator(IPendingRequestQueue inner, CancellationTokenSource cancellationTokenSource, Func<Task<bool>>? shouldCancel)
            {
                this.inner = inner;
                this.cancellationTokenSource = cancellationTokenSource;
                this.shouldCancel = shouldCancel;
            }

            public bool IsEmpty => inner.IsEmpty;
            public int Count => inner.Count;
            public async Task ApplyResponse(ResponseMessage response, ServiceEndPoint destination) => await inner.ApplyResponse(response, destination);
            public async Task<RequestMessageWithCancellationToken?> DequeueAsync(CancellationToken cancellationToken) => await inner.DequeueAsync(cancellationToken);

            public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken cancellationTokens)
            {
                var queueAndWait = inner.QueueAndWaitAsync(request, cancellationTokens);
                var cancel = Task.Run(async () =>
                {
                    if (await shouldCancel())
                    {
                        // Allow the PendingRRequest to be queued
                        cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(1));
                    }
                });

                await Task.WhenAll(queueAndWait, cancel);

                return await queueAndWait;
            }

            public ValueTask DisposeAsync()
            {
                return inner.DisposeAsync();
            }
        }
    }
}