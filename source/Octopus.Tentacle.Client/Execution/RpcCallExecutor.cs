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
        private static readonly TimeSpan AbandonAfter = TimeSpan.FromSeconds(5);
        public TimeSpan RetryTimeout => rpcCallRetryHandler.RetryTimeout;

        private readonly RpcCallRetryHandler rpcCallRetryHandler;
        private readonly RpcCallNoRetriesHandler rpcCallNoRetriesHandler;
        private readonly ITentacleClientObserver tentacleClientObserver;

        internal RpcCallExecutor(
            RpcCallRetryHandler rpcCallRetryHandler,
            RpcCallNoRetriesHandler rpcCallNoRetriesHandler,
            ITentacleClientObserver tentacleClientObserver)
        {
            this.rpcCallRetryHandler = rpcCallRetryHandler;
            this.rpcCallNoRetriesHandler = rpcCallNoRetriesHandler;
            this.tentacleClientObserver = tentacleClientObserver;
        }

        public async Task<T> ExecuteWithRetries<T>(
            RpcCall rpcCall,
            Func<CancellationToken, T> action,
            ILog logger,
            bool abandonActionOnCancellation,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            CancellationToken cancellationToken)
        {
            var rpcCallMetricsBuilder = RpcCallMetricsBuilder.StartWithRetries(rpcCall, rpcCallRetryHandler.RetryTimeout);

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
                        onRetryAction: async (lastException, sleepDuration, retryCount, totalRetryDuration, _) =>
                        {
                            await Task.CompletedTask;
                            logger.Info($"An error occurred communicating with Tentacle. This action will be retried after {sleepDuration.TotalSeconds} seconds. Retry attempt {retryCount}. Retries will be performed for up to {totalRetryDuration.TotalSeconds} seconds.");
                            logger.Verbose(lastException);
                        },
                        onTimeoutAction: async (timeoutDuration, _) =>
                        {
                            await Task.CompletedTask;
                            logger.Info($"Could not communicate with Tentacle after retrying for {timeoutDuration.TotalSeconds} seconds. No more retries will be attempted.");
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
                clientOperationMetricsBuilder.WithRpcCall(rpcCallMetrics);
                tentacleClientObserver.RpcCallCompleted(rpcCallMetrics);
            }
        }

        public async Task<T> ExecuteWithNoRetries<T>(
            RpcCall rpcCall,
            Func<CancellationToken, T> action,
            bool abandonActionOnCancellation,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            CancellationToken cancellationToken)
        {
            return await rpcCallNoRetriesHandler.ExecuteWithNoRetries(
                async ct =>
                {
                    // Wrap the action in a task so it doesn't block on sync Halibut calls
                    return await Task.Run(() =>
                    {
                        var rpcCallMetricsBuilder = RpcCallMetricsBuilder.StartWithoutRetries(rpcCall);
                        var start = DateTimeOffset.UtcNow;

                        try
                        {
                            var response = action(ct);
                            rpcCallMetricsBuilder.WithAttempt(TimedOperation.Success(start));
                            return response;
                        }
                        catch (Exception e)
                        {
                            rpcCallMetricsBuilder.WithAttempt(TimedOperation.Failure(start, e, ct));
                            rpcCallMetricsBuilder.Failure(e, ct);
                            throw;
                        }
                        finally
                        {
                            var rpcCallMetrics = rpcCallMetricsBuilder.Build();
                            clientOperationMetricsBuilder.WithRpcCall(rpcCallMetrics);
                            tentacleClientObserver.RpcCallCompleted(rpcCallMetrics);
                        }
                    }, ct);
                },
                abandonActionOnCancellation,
                AbandonAfter,
                cancellationToken);
        }

        public async Task ExecuteWithNoRetries(
            RpcCall rpcCall,
            Action<CancellationToken> action,
            bool abandonActionOnCancellation,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            CancellationToken cancellationToken)
        {
            await rpcCallNoRetriesHandler.ExecuteWithNoRetries(
                async ct =>
                {
                    var rpcCallMetricsBuilder = RpcCallMetricsBuilder.StartWithoutRetries(rpcCall);
                    var start = DateTimeOffset.UtcNow;

                    try
                    {
                        await Task.Run(() => action(ct), ct);
                        rpcCallMetricsBuilder.WithAttempt(TimedOperation.Success(start));
                    }
                    catch (Exception e)
                    {
                        rpcCallMetricsBuilder.WithAttempt(TimedOperation.Failure(start, e, ct));
                        rpcCallMetricsBuilder.Failure(e, ct);
                        throw;
                    }
                    finally
                    {
                        var rpcCallMetrics = rpcCallMetricsBuilder.Build();
                        clientOperationMetricsBuilder.WithRpcCall(rpcCallMetrics);
                        tentacleClientObserver.RpcCallCompleted(rpcCallMetrics);
                    }
                },
                abandonActionOnCancellation,
                AbandonAfter,
                cancellationToken);
        }
    }
}
