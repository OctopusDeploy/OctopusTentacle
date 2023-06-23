using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Client.Execution
{
    internal class RpcCallExecutor
    {
        private readonly RpcCallRetryHandler rpcCallRetryHandler;
        private readonly IRpcCallObserver rpcCallObserver;

        internal RpcCallExecutor(RpcCallRetryHandler rpcCallRetryHandler, IRpcCallObserver rpcCallObserver)
        {
            this.rpcCallRetryHandler = rpcCallRetryHandler;
            this.rpcCallObserver = rpcCallObserver;
        }

        public TimeSpan RetryTimeout => rpcCallRetryHandler.RetryTimeout;

        public async Task<T> ExecuteWithRetries<T>(
            string rpcCallName,
            Func<CancellationToken, T> action,
            ILog logger,
            bool abandonActionOnCancellation,
            CancellationToken cancellationToken)
        {
            var rpcCallMetricsBuilder = RpcCallMetricsBuilder.StartWithRetries(rpcCallName, rpcCallRetryHandler.RetryTimeout);

            try
            {
                var response = await rpcCallRetryHandler.ExecuteWithRetries(
                        async ct =>
                        {
                            var start = DateTimeOffset.UtcNow;

                            try
                            {
                                // Wrap the action in a task so it doesn't block on sync Halibut calls
                                var actionTask = Task.Run(() => action(ct), ct);

                                var response = await actionTask.ConfigureAwait(false);

                                rpcCallMetricsBuilder.WithAttempt(TimedOperation.Success(start));
                                return response;
                            }
                            catch (Exception e)
                            {
                                rpcCallMetricsBuilder.WithAttempt(TimedOperation.Failure(start, e, ct));
                                throw;
                            }
                        },
                        onRetryAction: async (lastException, sleepDuration, retryCount, totalRetryDuration, ct) =>
                        {
                            await Task.CompletedTask;
                            logger.Info($"An error occurred communicating with Tentacle. This action will be retried after {sleepDuration.TotalSeconds} seconds. Retry attempt {retryCount}. Retries will be performed for up to {totalRetryDuration.TotalSeconds} seconds.");
                            logger.Verbose(lastException);
                        },
                        onTimeoutAction: async (timeoutDuration, ct) =>
                        {
                            await Task.CompletedTask;
                            logger.Info($"Could not communicating with Tentacle after retrying for {timeoutDuration.TotalSeconds} seconds. No more retries will be attempted.");
                        },
                        abandonActionOnCancellation,
                        abandonAfter: TimeSpan.FromSeconds(5),
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
        }

        public T Execute<T>(
            string rpcCallName,
            Func<CancellationToken, T> action,
            CancellationToken cancellationToken)
        {
            var rpcCallMetricsBuilder = RpcCallMetricsBuilder.StartWithoutRetries(rpcCallName);

            try
            {
                var start = DateTimeOffset.UtcNow;

                try
                {
                    var response = action(cancellationToken);

                    rpcCallMetricsBuilder.WithAttempt(TimedOperation.Success(start));
                    return response;
                }
                catch (Exception e)
                {
                    rpcCallMetricsBuilder.WithAttempt(TimedOperation.Failure(start, e, cancellationToken));
                    throw;
                }
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
        }

        public void Execute(
            string rpcCallName,
            Action<CancellationToken> action,
            CancellationToken cancellationToken)
        {
            var rpcCallMetricsBuilder = RpcCallMetricsBuilder.StartWithoutRetries(rpcCallName);

            try
            {
                var start = DateTimeOffset.UtcNow;

                try
                {
                    action(cancellationToken);

                    rpcCallMetricsBuilder.WithAttempt(TimedOperation.Success(start));
                }
                catch (Exception e)
                {
                    rpcCallMetricsBuilder.WithAttempt(TimedOperation.Failure(start, e, cancellationToken));
                    throw;
                }
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
        }
    }
}