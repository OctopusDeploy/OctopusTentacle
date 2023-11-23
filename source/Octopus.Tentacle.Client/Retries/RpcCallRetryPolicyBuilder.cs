using System;
using System.Threading.Tasks;
using Halibut;
using Halibut.Diagnostics;
using Polly;
using Polly.Timeout;

namespace Octopus.Tentacle.Client.Retries
{
    internal class RpcCallRetryPolicyBuilder
    {
        TimeSpan retryTimeout = TimeSpan.FromSeconds(60);
        Func<Context, TimeSpan, Task, Exception, Task>? onTimeoutAction;
        Func<Exception, TimeSpan, int, Context, Task>? onRetryAction;

        public RpcCallRetryPolicyBuilder WithRetryTimeout(TimeSpan retryTimeout)
        {
            this.retryTimeout = retryTimeout;

            return this;
        }

        public RpcCallRetryPolicyBuilder WithOnTimeoutAction(Func<Context, TimeSpan, Task, Exception, Task> onTimeoutAction)
        {
            this.onTimeoutAction = onTimeoutAction;

            return this;
        }

        public RpcCallRetryPolicyBuilder WithOnRetryAction(Func<Exception, TimeSpan, int, Context, Task> onRetryAction)
        {
            this.onRetryAction = onRetryAction;

            return this;
        }

        public AsyncPolicy BuildTimeoutPolicy()
        {
            var timeoutPolicy = Policy
                .TimeoutAsync(
                    seconds: (int)retryTimeout.TotalSeconds,
                    timeoutStrategy: TimeoutStrategy.Optimistic,
                    onTimeoutAsync: onTimeoutAction ?? DefaultOnTimeoutAction);

            return timeoutPolicy;

            async Task DefaultOnTimeoutAction(Context context, TimeSpan timeout, Task task, Exception exception)
            {
                await Task.CompletedTask;
            }
        }

        public AsyncPolicy BuildRetryPolicy()
        {
            var handleAndRetryPolicy = Policy
                .Handle<HalibutClientException>(exceptionPredicate: ex => ex.IsNetworkError() != HalibutNetworkExceptionType.NotANetworkError)
                .WaitAndRetryAsync(
                    retryCount: int.MaxValue,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Min(retryAttempt, 10)),
                    onRetryAsync: onRetryAction ?? DefaultOnRetryAction);

            return handleAndRetryPolicy;

            async Task DefaultOnRetryAction(Exception exception, TimeSpan sleepDuration, int retryCount, Context context)
            {
                await Task.CompletedTask;
            }
        }

        public AsyncPolicy Build()
        {
            var timeoutPolicy = BuildTimeoutPolicy();

            var handleAndRetryPolicy = BuildRetryPolicy();

            var policyWrap = Policy.WrapAsync(timeoutPolicy, handleAndRetryPolicy);

            return policyWrap!;
        }
    }
}
