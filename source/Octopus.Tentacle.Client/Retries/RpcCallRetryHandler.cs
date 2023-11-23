using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Exceptions;
using Polly;
using Polly.Timeout;

namespace Octopus.Tentacle.Client.Retries
{
    internal class RpcCallRetryHandler
    {
        /// <summary>
        /// Action to Perform before a retry is attempted
        /// </summary>
        /// <param name="lastException">The last exception that occurred</param>
        /// <param name="retrySleepDuration">The duration execution will sleep for before the next retry attempt</param>
        /// <param name="retryCount">The current retry count</param>
        /// <param name="retryTimeout">The total duration that retries are allowed to take, including the initial execution</param>
        /// <param name="elapsedDuration">The duration that has elapsed already including the initial execution and any previous retries</param>
        /// <param name="cancellationToken">CancellationToken that will be cancelled when execution is cancelled by the caller</param>
        /// <returns></returns>
        public delegate Task OnRetryAction(Exception lastException, TimeSpan retrySleepDuration, int retryCount, TimeSpan retryTimeout, TimeSpan elapsedDuration, CancellationToken cancellationToken);

        public delegate Task OnTimeoutAction(TimeSpan retryTimeout, TimeSpan elapsedDuration, int retryCount, CancellationToken cancellationToken);

        public RpcCallRetryHandler(TimeSpan retryTimeout)
        {
            RetryTimeout = retryTimeout;
        }

        public TimeSpan RetryTimeout { get; }

        public TimeSpan RetryIfRemainingDurationAtLeast { get; } = TimeSpan.FromSeconds(1);

        public async Task<T> ExecuteWithRetries<T>(
            Func<CancellationToken, Task<T>> action,
            OnRetryAction? onRetryAction,
            OnTimeoutAction? onTimeoutAction,
            CancellationToken cancellationToken)
        {
            Exception? lastException = null;
            var started = new Stopwatch();
            var nextSleepDuration = TimeSpan.Zero;
            var totalRetryCount = 0;

            async Task OnRetryAction(Exception exception, TimeSpan sleepDuration, int retryCount, Context context)
            {
                lastException = exception;
                nextSleepDuration = sleepDuration;
                var elapsedDuration = started.Elapsed;
                var remainingRetryDuration = RetryTimeout - elapsedDuration - sleepDuration;

                if (ShouldRetryWithRemainingDuration(remainingRetryDuration))
                {
                    totalRetryCount = retryCount;

                    if (onRetryAction != null)
                    {
                        await onRetryAction.Invoke(exception, sleepDuration, retryCount, RetryTimeout, elapsedDuration, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            async Task OnTimeoutAction(Context? context, TimeSpan timeout, Task? task, Exception? exception)
            {
                var elapsedDuration = started.Elapsed;

                if (onTimeoutAction != null)
                {
                    await onTimeoutAction.Invoke(RetryTimeout, elapsedDuration, totalRetryCount, cancellationToken).ConfigureAwait(false);
                }
            }

            var policyBuilder = new RpcCallRetryPolicyBuilder()
                .WithRetryTimeout(RetryTimeout)
                .WithOnRetryAction(OnRetryAction)
                .WithOnTimeoutAction(OnTimeoutAction);

            var retryPolicy = policyBuilder.BuildRetryPolicy();
            var isInitialAction = true;

            // This ensures the timeout policy does not apply to the initial request and only applies to retries
            async Task<T> ExecuteAction(CancellationToken ct)
            {
                if (isInitialAction)
                {
                    isInitialAction = false;
                    return await action(ct).ConfigureAwait(false);
                }

                var remainingRetryDuration = RetryTimeout - started.Elapsed - nextSleepDuration;

                if (!ShouldRetryWithRemainingDuration(remainingRetryDuration))
                {
                    // We are short circuiting as the retry duration has elapsed
                    await OnTimeoutAction(null, RetryTimeout, null, null).ConfigureAwait(false);
                    throw new TimeoutRejectedException("The delegate executed asynchronously through TimeoutPolicy did not complete within the timeout.");
                }

                var timeoutPolicy = policyBuilder
                    // Ensure the remaining retry time excludes the elapsed time
                    .WithRetryTimeout(remainingRetryDuration)
                    .BuildTimeoutPolicy();

                return await timeoutPolicy.ExecuteAsync(action, ct).ConfigureAwait(false);
            }

            try
            {
                started.Start();
                return await retryPolicy.ExecuteAsync(ExecuteAction, cancellationToken);
            }
            catch (Exception ex) when (ex is TimeoutRejectedException or TaskCanceledException)
            {
                // If the timeout policy timed out or the cancellation token caused a generic task cancellation 
                // and we have captured an exception from a previous retry, then throw the more meaningful previous exception.
                if (lastException != null)
                {
                    throw lastException;
                }

                throw;
            }

            bool ShouldRetryWithRemainingDuration(TimeSpan remainingRetryDuration)
            {
                return remainingRetryDuration > RetryIfRemainingDurationAtLeast;
            }
        }
    }
}
