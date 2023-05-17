using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Timeout;

namespace Octopus.Tentacle.Client.Retries
{
    internal class RpcCallRetryHandler
    {
        readonly TimeoutStrategy timeoutStrategy;

        public RpcCallRetryHandler(TimeSpan retryTimeout, TimeoutStrategy timeoutStrategy)
        {
            this.timeoutStrategy = timeoutStrategy;
            RetryTimeout = retryTimeout;
        }

        public TimeSpan RetryTimeout { get; }

        public async Task<T> ExecuteWithRetries<T>(
            Func<CancellationToken, Task<T>> action,
            Func<Exception, TimeSpan, int, TimeSpan, CancellationToken, Task>? onRetryAction,
            Func<TimeSpan, CancellationToken, Task>? onTimeoutAction,
            CancellationToken cancellationToken)
        {
            Exception? lastException = null;

            async Task OnRetryAction(Exception exception, TimeSpan sleepDuration, int retryCount, Context context)
            {
                await Task.CompletedTask;
                lastException = exception;

                if (onRetryAction != null)
                {
                    await onRetryAction.Invoke(exception, sleepDuration, retryCount, RetryTimeout, cancellationToken);
                }
            }

            async Task OnTimeoutAction(Context? context, TimeSpan timeout, Task? task, Exception? exception)
            {
                await Task.CompletedTask;

                if (onTimeoutAction != null)
                {
                    await onTimeoutAction.Invoke(RetryTimeout, cancellationToken);
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
                    return await action(ct);
                }

                var remainingRetryDuration = RetryTimeout - started.Elapsed;

                if (remainingRetryDuration < TimeSpan.FromSeconds(1))
                {
                    // We are short circuiting as the retry duration has elapsed
                    await OnTimeoutAction(null, RetryTimeout, null, null);
                    throw new TimeoutRejectedException("The delegate executed asynchronously through TimeoutPolicy did not complete within the timeout.");
                }

                var timeoutPolicy = policyBuilder
                    // Ensure the remaining retry time excludes the elapsed time
                    .WithRetryTimeout(remainingRetryDuration, timeoutStrategy)
                    .BuildTimeoutPolicy();

                return await timeoutPolicy.ExecuteAsync(action, ct);
            }

            try
            {
                started.Start();
                return await retryPolicy.ExecuteAsync(ExecuteAction, cancellationToken);
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
    }
}
