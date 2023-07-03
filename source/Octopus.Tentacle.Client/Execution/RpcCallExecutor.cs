using System;
using System.Reflection.Emit;
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
        private readonly ITentacleClientObserver tentacleClientObserver;

        internal RpcCallExecutor(RpcCallRetryHandler rpcCallRetryHandler, ITentacleClientObserver tentacleClientObserver)
        {
            this.rpcCallRetryHandler = rpcCallRetryHandler;
            this.tentacleClientObserver = tentacleClientObserver;
        }

        public TimeSpan RetryTimeout => rpcCallRetryHandler.RetryTimeout;

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

        public T Execute<T>(
            RpcCall rpcCall,
            Func<CancellationToken, T> action,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            CancellationToken cancellationToken)
        {
            return Execute(rpcCall, action, abandonActionOnCancellation: false, clientOperationMetricsBuilder, cancellationToken);
        }

        public T Execute<T>(
            RpcCall rpcCall,
            Func<CancellationToken, T> action,
            bool abandonActionOnCancellation,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            CancellationToken cancellationToken)
        {
            var rpcCallMetricsBuilder = RpcCallMetricsBuilder.StartWithoutRetries(rpcCall);
            var start = DateTimeOffset.UtcNow;
            var abandonAfter = TimeSpan.FromSeconds(5);

            T ExecuteAction(Action<Exception> exceptionHandler)
            {
                try
                {
                    var response = action(cancellationToken);

                    rpcCallMetricsBuilder.WithAttempt(TimedOperation.Success(start));
                    return response;
                }
                catch (Exception e)
                {
                    rpcCallMetricsBuilder.WithAttempt(TimedOperation.Failure(start, e, cancellationToken));
                    rpcCallMetricsBuilder.Failure(e, cancellationToken);

                    exceptionHandler(e);

                    throw;
                }
                finally
                {
                    var rpcCallMetrics = rpcCallMetricsBuilder.Build();
                    clientOperationMetricsBuilder.WithRpcCall(rpcCallMetrics);
                    tentacleClientObserver.RpcCallCompleted(rpcCallMetrics);
                }
            }

            if (!abandonActionOnCancellation)
            {
                return ExecuteAction(_ =>
                {
                });
            }

            using var abandonCancellationTokenSource = new CancellationTokenSource();
            using (cancellationToken.Register(() =>
                   {
                       abandonCancellationTokenSource.TryCancelAfter(abandonAfter);
                   }))
            {
                return ExecuteAction(e =>
                {
                    if (e is OperationCanceledException && abandonCancellationTokenSource.IsCancellationRequested)
                    {
                        throw new OperationAbandonedException(e, abandonAfter);
                    }
                });
            }
        }

        public void Execute(
            RpcCall rpcCall,
            Action<CancellationToken> action,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            CancellationToken cancellationToken)
        {
            Execute(
                rpcCall,
                action,
                abandonActionOnCancellation: false,
                clientOperationMetricsBuilder,
                cancellationToken);
        }

        public void Execute(
            RpcCall rpcCall,
            Action<CancellationToken> action,
            bool abandonActionOnCancellation,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            CancellationToken cancellationToken)
        {
            var rpcCallMetricsBuilder = RpcCallMetricsBuilder.StartWithoutRetries(rpcCall);
            var start = DateTimeOffset.UtcNow;
            var abandonAfter = TimeSpan.FromSeconds(5);

            void ExecuteAction(Action<Exception> exceptionHandler)
            {
                try
                {
                    action(cancellationToken);

                    rpcCallMetricsBuilder.WithAttempt(TimedOperation.Success(start));
                }
                catch (Exception e)
                {
                    rpcCallMetricsBuilder.WithAttempt(TimedOperation.Failure(start, e, cancellationToken));
                    rpcCallMetricsBuilder.Failure(e, cancellationToken);

                    exceptionHandler(e);

                    throw;
                }
                finally
                {
                    var rpcCallMetrics = rpcCallMetricsBuilder.Build();
                    clientOperationMetricsBuilder.WithRpcCall(rpcCallMetrics);
                    tentacleClientObserver.RpcCallCompleted(rpcCallMetrics);
                }
            }

            if (!abandonActionOnCancellation)
            {
                ExecuteAction(_ => { });
                return;
            }
            
            using var abandonCancellationTokenSource = new CancellationTokenSource();
            using (cancellationToken.Register(() =>
                   {
                       abandonCancellationTokenSource.TryCancelAfter(abandonAfter);
                   }))
            {
                ExecuteAction(e =>
                {
                    if (e is OperationCanceledException && abandonCancellationTokenSource.IsCancellationRequested)
                    {
                        throw new OperationAbandonedException(e, abandonAfter);
                    }
                });
            }
            
        }
    }
}
