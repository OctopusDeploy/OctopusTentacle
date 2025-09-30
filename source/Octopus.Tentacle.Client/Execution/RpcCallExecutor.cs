using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Contracts.Logging;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Client.Execution
{
    internal class RpcCallExecutor
    {
        public TimeSpan RetryTimeout => rpcCallRetryHandler.RetryTimeout;

        readonly RpcCallRetryHandler rpcCallRetryHandler;
        readonly ITentacleClientObserver tentacleClientObserver;

        internal RpcCallExecutor(
            RpcCallRetryHandler rpcCallRetryHandler,
            ITentacleClientObserver tentacleClientObserver)
        {
            this.rpcCallRetryHandler = rpcCallRetryHandler;
            this.tentacleClientObserver = tentacleClientObserver;
        }

        public async Task<T> Execute<T>(
            bool retriesEnabled,
            RpcCall rpcCall,
            Func<CancellationToken, Task<T>> action,
            ITentacleClientTaskLog logger,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            CancellationToken cancellationToken)
        {
            return await Execute(retriesEnabled, rpcCall, action, null, logger, clientOperationMetricsBuilder, cancellationToken);
        }

        public async Task<T> Execute<T>(
            bool retriesEnabled,
            RpcCall rpcCall,
            Func<CancellationToken, Task<T>> action,
            Action<Exception>? onErrorAction,
            ITentacleClientTaskLog logger,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            CancellationToken cancellationToken)
        {
            return retriesEnabled
                ? await ExecuteWithRetries(rpcCall, action, onErrorAction, logger, clientOperationMetricsBuilder, cancellationToken)
                : await ExecuteWithNoRetries(rpcCall, action, logger, clientOperationMetricsBuilder, cancellationToken);
        }

        public async Task<T> ExecuteWithRetries<T>(
            RpcCall rpcCall,
            Func<CancellationToken, Task<T>> action,
            Action<Exception>? onErrorAction,
            ITentacleClientTaskLog logger,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            CancellationToken cancellationToken)
        {
            using var activity = TentacleClient.ActivitySource.StartActivity($"{nameof(RpcCallExecutor)}.{nameof(ExecuteWithRetries)}");
            activity?.AddTag("octopus.tentacle.rpc_call.service", rpcCall.Service);
            activity?.AddTag("octopus.tentacle.rpc_call.name", rpcCall.Name);
            var rpcCallMetricsBuilder = RpcCallMetricsBuilder.StartWithRetries(rpcCall, rpcCallRetryHandler.RetryTimeout);

            try
            {
                var response = await rpcCallRetryHandler.ExecuteWithRetries(
                        async ct =>
                        {
                            var start = DateTimeOffset.UtcNow;

                            try
                            {
                                var response = await action(ct);

                                rpcCallMetricsBuilder.WithAttempt(TimedOperation.Success(start));
                                return response;
                            }
                            catch (Exception e)
                            {
                                rpcCallMetricsBuilder.WithAttempt(TimedOperation.Failure(start, e, ct));
                                throw;
                            }
                        },
                        onRetryAction: async (lastException, sleepDuration, retryCount, totalRetryDuration, elapsedDuration, _) =>
                        {
                            await Task.CompletedTask;

                            onErrorAction?.Invoke(lastException);

                            var remainingDurationInSeconds = Math.Max((int)(totalRetryDuration - elapsedDuration).TotalSeconds, 0);
                            logger.Info($"An error occurred communicating with Tentacle. This action will be retried after {(int)sleepDuration.TotalSeconds} seconds. Retry attempt {retryCount}. Retries will be performed for up to {remainingDurationInSeconds} seconds.");
                            logger.Verbose(lastException);
                        },
                        onTimeoutAction: async (timeoutDuration, elapsedDuration, retryCount, _) =>
                        {
                            await Task.CompletedTask;

                            if (retryCount > 0)
                            {
                                logger.Info($"Could not communicate with Tentacle after {(int)elapsedDuration.TotalSeconds} seconds. No more retries will be attempted.");
                            }
                            else
                            {
                                logger.Info($"Could not communicate with Tentacle after {(int)elapsedDuration.TotalSeconds} seconds.");
                            }
                        },
                        cancellationToken);

                return response;
            }
            catch (Exception e)
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                // We should use activity.AddException here, but need to update the referenced version of System.Diagnostics.DiagnosticSource.
                // We inherit the reference from Halibut so that would have to change first.
                activity?.AddTag("exception.message", e.Message); 
                rpcCallMetricsBuilder.Failure(e, cancellationToken);
                throw;
            }
            finally
            {
                var rpcCallMetrics = rpcCallMetricsBuilder.Build();
                clientOperationMetricsBuilder.WithRpcCall(rpcCallMetrics);
                tentacleClientObserver.RpcCallCompleted(rpcCallMetrics, logger);
            }
        }

        public async Task<T> ExecuteWithNoRetries<T>(
            RpcCall rpcCall,
            Func<CancellationToken, Task<T>> action,
            ITentacleClientTaskLog logger,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            CancellationToken cancellationToken)
        {
            using var activity = TentacleClient.ActivitySource.StartActivity($"{nameof(RpcCallExecutor)}.{nameof(ExecuteWithNoRetries)}");
            activity?.AddTag("octopus.tentacle.rpc_call.service", rpcCall.Service);
            activity?.AddTag("octopus.tentacle.rpc_call.name", rpcCall.Name);
            var rpcCallMetricsBuilder = RpcCallMetricsBuilder.StartWithoutRetries(rpcCall);
            var start = DateTimeOffset.UtcNow;

            try
            {
                var response = await action(cancellationToken).ConfigureAwait(false);
                rpcCallMetricsBuilder.WithAttempt(TimedOperation.Success(start));
                return response;
            }
            catch (Exception e)
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                // We should use activity.AddException here, but need to update the referenced version of System.Diagnostics.DiagnosticSource.
                // We inherit the reference from Halibut so that would have to change first.
                activity?.AddTag("exception.message", e.Message); 
                
                rpcCallMetricsBuilder.WithAttempt(TimedOperation.Failure(start, e, cancellationToken));
                rpcCallMetricsBuilder.Failure(e, cancellationToken);
                throw;
            }
            finally
            {
                var rpcCallMetrics = rpcCallMetricsBuilder.Build();
                clientOperationMetricsBuilder.WithRpcCall(rpcCallMetrics);
                tentacleClientObserver.RpcCallCompleted(rpcCallMetrics, logger);
            }
        }


        public async Task ExecuteWithNoRetries(
            RpcCall rpcCall,
            Func<CancellationToken, Task> action,
            ITentacleClientTaskLog logger,
            ClientOperationMetricsBuilder clientOperationMetricsBuilder,
            CancellationToken cancellationToken)
        {
            using var activity = TentacleClient.ActivitySource.StartActivity($"{nameof(RpcCallExecutor)}.{nameof(ExecuteWithNoRetries)}");
            activity?.AddTag("octopus.tentacle.rpc_call.service", rpcCall.Service);
            activity?.AddTag("octopus.tentacle.rpc_call.name", rpcCall.Name);
            var rpcCallMetricsBuilder = RpcCallMetricsBuilder.StartWithoutRetries(rpcCall);
            var start = DateTimeOffset.UtcNow;

            try
            {
                await action(cancellationToken).ConfigureAwait(false);
                rpcCallMetricsBuilder.WithAttempt(TimedOperation.Success(start));
            }
            catch (Exception e)
            {
                activity?.SetStatus(ActivityStatusCode.Error);
                // We should use activity.AddException here, but need to update the referenced version of System.Diagnostics.DiagnosticSource.
                // We inherit the reference from Halibut so that would have to change first.
                activity?.AddTag("exception.message", e.Message); 
                rpcCallMetricsBuilder.WithAttempt(TimedOperation.Failure(start, e, cancellationToken));
                rpcCallMetricsBuilder.Failure(e, cancellationToken);
                throw;
            }
            finally
            {
                var rpcCallMetrics = rpcCallMetricsBuilder.Build();
                clientOperationMetricsBuilder.WithRpcCall(rpcCallMetrics);
                tentacleClientObserver.RpcCallCompleted(rpcCallMetrics, logger);
            }
        }
    }
}
