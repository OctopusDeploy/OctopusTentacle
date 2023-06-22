using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client.Scripts.Observability;

namespace Octopus.Tentacle.Client.Retries
{
    internal class RpcCallRetryHandlerObservabilityDecorator : IRpcCallRetryHandler
    {
        private readonly IRpcCallRetryHandler inner;
        private readonly IRpcCallObserver rpcCallObserver;

        internal RpcCallRetryHandlerObservabilityDecorator(IRpcCallRetryHandler inner, IRpcCallObserver rpcCallObserver)
        {
            this.inner = inner;
            this.rpcCallObserver = rpcCallObserver;
        }

        public TimeSpan RetryTimeout => inner.RetryTimeout;

        public async Task<T> ExecuteWithRetries<T>(
            string rcpCallName,
            Func<CancellationToken, Task<T>> action, 
            RpcCallRetryHandler.OnRetyAction? onRetryAction,
            RpcCallRetryHandler.OnTimeoutAction? onTimeoutAction,
            bool abandonActionOnCancellation, 
            TimeSpan abandonAfter, 
            CancellationToken cancellationToken)
        {
            var rpcCallMetricsBuilder = RpcCallMetricsBuilder.Start(rcpCallName, RetryTimeout);

            try
            {
                var response = await inner.ExecuteWithRetries(
                    rcpCallName,
                    async ct => await CallActionWithMetricsGathering(ct),
                    onRetryAction,
                    onTimeoutAction,
                    abandonActionOnCancellation,
                    abandonAfter,
                    cancellationToken)
                    .ConfigureAwait(false);
                return response;
            }
            catch (Exception e)
            {
                rpcCallMetricsBuilder.Failure(e, cancellationToken);
                throw;
            }
            finally
            {
                var rpcCallMetrics = rpcCallMetricsBuilder.Build();
                rpcCallObserver.RpcCallCompleted(rpcCallMetrics);
            }

            async Task<T> CallActionWithMetricsGathering(CancellationToken ct)
            {
                var start = DateTimeOffset.UtcNow;

                try
                {
                    var response = await action(ct).ConfigureAwait(false);

                    rpcCallMetricsBuilder.WithAttempt(TimedOperation.Success(start));
                    return response;
                }
                catch (Exception e)
                {
                    rpcCallMetricsBuilder.WithAttempt(TimedOperation.Failure(start, e, ct));
                    throw;
                }
            }
        }
    }
}