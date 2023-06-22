using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Client.Retries
{
    internal static class RpcCallRetryHandlerExtensionMethods
    {
        public static async Task<T> ExecuteWithRetries<T>(
            this IRpcCallRetryHandler rpcCallRetryHandler,
            string rpcCallName,
            Func<CancellationToken, T> action,
            ILog logger,
            CancellationToken cancellationToken,
            bool abandonActionOnCancellation)
        {
            return await rpcCallRetryHandler.ExecuteWithRetries(
                rpcCallName,
                async ct =>
                {
                    // Wrap the action in a task so it doesn't block on sync Halibut calls
                    var actionTask = Task.Run(() => action(ct), ct);

                    return await actionTask.ConfigureAwait(false);
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
                cancellationToken);
        }
    }
}