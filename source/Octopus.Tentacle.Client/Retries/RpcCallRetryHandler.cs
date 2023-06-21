using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Timeout;

namespace Octopus.Tentacle.Client.Retries
{
    internal class RpcCallRetryHandler : IRpcCallRetryHandler
    {
        public delegate Task OnRetyAction(Exception lastException, TimeSpan retrySleepDuration, int retryCount, TimeSpan retryTimeout, CancellationToken cancellationToken);
        public delegate Task OnTimeoutAction(TimeSpan retryTimeout, CancellationToken cancellationToken);

        readonly TimeoutStrategy timeoutStrategy;

        public RpcCallRetryHandler(TimeSpan retryTimeout, TimeoutStrategy timeoutStrategy)
        {
            this.timeoutStrategy = timeoutStrategy;
            RetryTimeout = retryTimeout;
        }

        public TimeSpan RetryTimeout { get; }

        public async Task<T> ExecuteWithRetries<T>(
            Func<CancellationToken, Task<T>> action,
            OnRetyAction? onRetryAction,
            OnTimeoutAction? onTimeoutAction,
            CancellationToken cancellationToken)
        {
            Exception? lastException = null;

            async Task OnRetryAction(Exception exception, TimeSpan sleepDuration, int retryCount, Context context)
            {
                lastException = exception;

                if (onRetryAction != null)
                {
                    await onRetryAction.Invoke(exception, sleepDuration, retryCount, RetryTimeout, cancellationToken).ConfigureAwait(false);
                }
            }

            async Task OnTimeoutAction(Context? context, TimeSpan timeout, Task? task, Exception? exception)
            {
                if (onTimeoutAction != null)
                {
                    await onTimeoutAction.Invoke(RetryTimeout, cancellationToken).ConfigureAwait(false);
                }
            }

            var policyBuilder = new RpcCallRetryPolicyBuilder()
                .WithRetryTimeout(RetryTimeout, timeoutStrategy)
                .WithOnRetryAction(OnRetryAction)
                .WithOnTimeoutAction(OnTimeoutAction);

            var retryPolicy = policyBuilder.BuildRetryPolicy();
            var isInitialAction = true;
            var started = new Stopwatch();

            // This ensures the timeout policy does not apply to the initial request and only applies to retries
            async Task<T> ExecuteAction(CancellationToken ct)
            {
                if (isInitialAction)
                {
                    isInitialAction = false;
                    return await action(ct).ConfigureAwait(false);
                }

                var remainingRetryDuration = RetryTimeout - started.Elapsed;

                if (remainingRetryDuration < TimeSpan.FromSeconds(1))
                {
                    // We are short circuiting as the retry duration has elapsed
                    await OnTimeoutAction(null, RetryTimeout, null, null).ConfigureAwait(false);
                    throw new TimeoutRejectedException("The delegate executed asynchronously through TimeoutPolicy did not complete within the timeout.");
                }

                var timeoutPolicy = policyBuilder
                    // Ensure the remaining retry time excludes the elapsed time
                    .WithRetryTimeout(remainingRetryDuration, timeoutStrategy)
                    .BuildTimeoutPolicy();

                return await timeoutPolicy.ExecuteAsync(action, ct).ConfigureAwait(false);
            }

            try
            {
                started.Start();
                return await retryPolicy.ExecuteAsync(ExecuteAction, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutRejectedException)
            {
                if (lastException != null)
                {
                    throw lastException;
                }

                throw;
            }
        }

        public async Task<T> ExecuteWithRetries<T>(
            Func<CancellationToken, Task<T>> action,
            OnRetyAction? onRetryAction,
            OnTimeoutAction? onTimeoutAction,
            bool abandonActionOnCancellation,
            TimeSpan abandonAfter,
            CancellationToken cancellationToken)
        {
            return await ExecuteWithRetries(
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
                onRetryAction,
                onTimeoutAction,
                cancellationToken);
        }
    }
}
