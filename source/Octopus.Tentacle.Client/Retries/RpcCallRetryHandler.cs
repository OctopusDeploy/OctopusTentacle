using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Client.Scripts;
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

        public RpcCallRetryHandler(TimeSpan retryTimeout, int? minimumAttemptsForInterruptedLongRunningCalls = null)
        {
            RetryTimeout = retryTimeout;
            MinimumAttemptsForInterruptedLongRunningCalls = minimumAttemptsForInterruptedLongRunningCalls;
        }

        public TimeSpan RetryTimeout { get; }

        /// <summary>
        /// This is the minimum number of attempts that will be made to long-running calls,
        /// that are interrupted while executing.
        /// For example, if a long-running file upload is interrupted after ten minutes of transfering,
        /// and this is set to 2, we will make another attempt to upload the file even if the RetryTimeout
        /// is exceeded.
        /// If this is set to 9999, and a polling tentacle did not collect the file upload request from the queue,
        /// we will not continue to make attempts until we have made 9999 attempts. This is because the RPC call
        /// is not considered to be interrupted as the RPC call never started. This means we won't try 9999 times
        /// to send a request to a tentacle that is not connected.
        /// </summary>
        public int? MinimumAttemptsForInterruptedLongRunningCalls { get; }

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
            var totalAttemptCount = 0;
            bool shouldExecuteNextRetryUnderTimeout = true;

            async Task OnRetryAction(Exception exception, TimeSpan sleepDuration, int retryCount, Context context)
            {
                // If tentacle was online (by virtue of NOT getting a connecting exception) AND we have been told
                // to make a min number of attempts, then our next attempt (if any) should be done without a timeout.
                // However, if tentacle was to be offline, then we would want to revert to retries being done under a timeout
                // so that we limit how long we try to communicate with a tentacle offline.
                // We must evaluate this each an attempt fails.
                shouldExecuteNextRetryUnderTimeout = !(MinimumAttemptsForInterruptedLongRunningCalls.HasValue && !exception.IsConnectionException());
                
                if (lastException == null)
                {
                    lastException = exception;
                }
                // Cancellation/Retry Timeout will result in a TransferringRequestCancelledException or ConnectingRequestCancelledException
                // which will hide the original exception. We ignore a cancellation/retry timeout triggered exception so we don't hide the last real exception
                // that occurred during the retry process
                else if(cancellationToken.IsCancellationRequested || !exception.IsConnectingOrTransferringRequestCancelledException())
                {
                    lastException = exception;
                }
                
                nextSleepDuration = sleepDuration;
                var elapsedDuration = started.Elapsed;
                var remainingRetryDuration = RetryTimeout - elapsedDuration - sleepDuration;

                if (ShouldRetry(remainingRetryDuration, totalAttemptCount, !shouldExecuteNextRetryUnderTimeout))
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
                    totalAttemptCount++;
                    isInitialAction = false;
                    return await action(ct).ConfigureAwait(false);
                }

                var remainingRetryDuration = RetryTimeout - started.Elapsed - nextSleepDuration;

                if (!ShouldRetry(remainingRetryDuration, totalAttemptCount, !shouldExecuteNextRetryUnderTimeout))
                {
                    // We are short circuiting as the retry duration has elapsed and minimum attempts have been satisfied
                    await OnTimeoutAction(null, RetryTimeout, null, null).ConfigureAwait(false);
                    throw new TimeoutRejectedException("The delegate executed asynchronously through TimeoutPolicy did not complete within the timeout.");
                }

                totalAttemptCount++;
                
                if (!shouldExecuteNextRetryUnderTimeout)
                {
                    // Retry a long-running operation with no timeout
                    return await action(ct);
                }
                else
                {
                    var timeoutPolicy = policyBuilder
                        // Ensure the remaining retry time excludes the elapsed time
                        .WithRetryTimeout(remainingRetryDuration)
                        .BuildTimeoutPolicy();
                
                
                    return await timeoutPolicy.ExecuteAsync(action, ct).ConfigureAwait(false);
                }
            }

            try
            {
                started.Start();
                return await retryPolicy.ExecuteAsync(ExecuteAction, cancellationToken);
            }
            catch (Exception ex) when (ex is TimeoutRejectedException or TaskCanceledException || 
                                       ex.IsConnectingOrTransferringRequestCancelledException())
            {
                // If the timeout policy timed out or the cancellation token caused a generic task cancellation 
                // and we have captured an exception from a previous retry, then throw the more meaningful previous exception.
                if (lastException != null)
                {
                    throw lastException;
                }

                throw;
            }

            bool ShouldRetry(TimeSpan remainingRetryDuration, int currentAttemptCount, bool retryRegardlessOfTimeout)
            {
                if (MinimumAttemptsForInterruptedLongRunningCalls.HasValue
                    && currentAttemptCount < MinimumAttemptsForInterruptedLongRunningCalls.Value
                    && retryRegardlessOfTimeout) return true;
                
                return remainingRetryDuration > RetryIfRemainingDurationAtLeast;
            }
        }
    }
}
