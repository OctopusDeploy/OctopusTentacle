using System;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Client.Retries
{
    internal static class RpcCallRetryHandlerExtensionMethods
    {
        public static async Task<T> ExecuteWithRetries<T>(
            this RpcCallRetryHandler rpcCallRetryHandler,
            Func<CancellationToken, T> action,
            ILog logger,
            CancellationToken cancellationToken,
            bool abandonActionOnCancellation)
        {
            return await rpcCallRetryHandler.ExecuteWithRetries(
                async ct =>
                {
                    var actionTask = Task.Run(() => action(ct), ct);

                    if (!abandonActionOnCancellation)
                    {
                        return await actionTask.ConfigureAwait(false);
                    }

                    var abandonAfter = TimeSpan.FromSeconds(5);

                    using var abandonCancellationTokenSource = new CancellationTokenSource();
                    using (ct.Register(() =>
                    {
                        // Give the actionTask some time to cancel on it's own.
                        // If it doesn't assume it does not co-operate with cancellationTokens and walk away.
                        abandonCancellationTokenSource.TryCancelAfter(abandonAfter);
                    }))
                    {
                        var abandonTask = abandonCancellationTokenSource.Token.AsTask<T>();

                        try
                        {
                            return await (await Task.WhenAny(actionTask, abandonTask).ConfigureAwait(false)).ConfigureAwait(false);
                        }
                        catch (Exception e) when (e is OperationCanceledException)
                        {
                            if (abandonCancellationTokenSource.IsCancellationRequested)
                            {
                                throw new OperationAbandonedException(e, abandonAfter);
                            }

                            throw;
                        }
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
                cancellationToken);
        }
    }
}