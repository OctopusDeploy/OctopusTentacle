using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client.Scripts.Observability;

namespace Octopus.Tentacle.Client.Retries
{
    internal class RpcCallRetryHandlerObservabilityDecorator : IRpcCallRetryHandler
    {
        private readonly RpcCallRetryHandler inner;
        private readonly IRpcCallObserver rpcCallObserver;

        internal RpcCallRetryHandlerObservabilityDecorator(RpcCallRetryHandler inner, IRpcCallObserver rpcCallObserver)
        {
            this.inner = inner;
            this.rpcCallObserver = rpcCallObserver;
        }

        public TimeSpan RetryTimeout => inner.RetryTimeout;

        public async Task<T> ExecuteWithRetries<T>(
            Func<CancellationToken, Task<T>> action, 
            RpcCallRetryHandler.OnRetyAction? onRetryAction, 
            RpcCallRetryHandler.OnTimeoutAction? onTimeoutAction, 
            CancellationToken cancellationToken)
        {
            var rpcCallMetricsBuilder = RpcCallMetricsBuilder.Start(RetryTimeout);

            try
            {
                var response = await inner.ExecuteWithRetries(
                    async ct =>
                    {
                        rpcCallMetricsBuilder.StartAttempt();

                        try
                        {
                            var response = await action(ct).ConfigureAwait(false);

                            rpcCallMetricsBuilder.AttemptSuccessful();
                            return response;
                        }
                        catch (Exception e)
                        {
                            rpcCallMetricsBuilder.AttemptFailed(e, ct);
                            throw;
                        }
                    },
                    onRetryAction,
                    onTimeoutAction,
                    cancellationToken)
                    .ConfigureAwait(false);
                
                return response;
            }
            catch (Exception e)
            {
                rpcCallMetricsBuilder.Failure(e);
                throw;
            }
            finally
            {
                var rpcCallMetrics = rpcCallMetricsBuilder.Build();
                rpcCallObserver.RpcCallCompleted(rpcCallMetrics);
            }
        }

        public async Task<T> ExecuteWithRetries<T>(
            Func<CancellationToken, Task<T>> action, 
            RpcCallRetryHandler.OnRetyAction? onRetryAction,
            RpcCallRetryHandler.OnTimeoutAction? onTimeoutAction,
            bool abandonActionOnCancellation, 
            TimeSpan abandonAfter, 
            CancellationToken cancellationToken)
        {
            var rpcCallMetricsBuilder = RpcCallMetricsBuilder.Start(RetryTimeout);

            try
            {
                var response = await inner.ExecuteWithRetries(
                    async ct =>
                    {
                        rpcCallMetricsBuilder.StartAttempt();

                        try
                        {
                            var response = await action(ct).ConfigureAwait(false);

                            rpcCallMetricsBuilder.AttemptSuccessful();
                            return response;
                        }
                        catch (Exception e)
                        {
                            rpcCallMetricsBuilder.AttemptFailed(e);
                            throw;
                        }
                    },
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
                rpcCallMetricsBuilder.Failure(e);
                throw;
            }
            finally
            {
                var rpcCallMetrics = rpcCallMetricsBuilder.Build();
                rpcCallObserver.RpcCallCompleted(rpcCallMetrics);
            }
        }
    }
}