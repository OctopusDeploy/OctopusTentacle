using System;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.Tentacle.Client.Retries
{
    public class RpcCallNoRetriesHandler
    {
        public async Task<T> ExecuteWithNoRetries<T>(
            Func<CancellationToken, Task<T>> action,
            CancellationToken cancellationToken)
        {
            return await action(cancellationToken).ConfigureAwait(false);
        }

        public async Task<T> ExecuteWithNoRetries<T>(
            Func<CancellationToken, Task<T>> action,
            bool abandonActionOnCancellation,
            TimeSpan abandonAfter,
            CancellationToken cancellationToken)
        {
            return await ExecuteWithNoRetries(
                async ct =>
                {
                    if (!abandonActionOnCancellation)
                    {
                        return await action(ct).ConfigureAwait(false);
                    }

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
                            var actionTask = action(ct);
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
                cancellationToken);
        }
    }
}
