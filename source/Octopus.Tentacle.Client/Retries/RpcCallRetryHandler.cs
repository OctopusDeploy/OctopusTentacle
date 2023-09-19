﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
        public delegate Task OnRetyAction(Exception lastException, TimeSpan retrySleepDuration, int retryCount, TimeSpan retryTimeout, TimeSpan elapsedDuration, CancellationToken cancellationToken);

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
            var started = new Stopwatch();

            async Task OnRetryAction(Exception exception, TimeSpan sleepDuration, int retryCount, Context context)
            {
                lastException = exception;
                var elapsedDuration = started.Elapsed;

                if (onRetryAction != null)
                {
                    await onRetryAction.Invoke(exception, sleepDuration, retryCount, RetryTimeout, elapsedDuration, cancellationToken).ConfigureAwait(false);
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

                    var actionTask = action(ct);

                    var actionTaskCompleted = await actionTask.WaitTillCompletedOrAbandoned(abandonAfter, cancellationToken);
                    if (!actionTaskCompleted)
                    {
                        //TODO: How important is the stack trace when this was within the try/catch?
                        throw new OperationAbandonedException(abandonAfter);
                    }

                    return await actionTask.ConfigureAwait(false);
                },
                onRetryAction,
                onTimeoutAction,
                cancellationToken);
        }
    }
}
