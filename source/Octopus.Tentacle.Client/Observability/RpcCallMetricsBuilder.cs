using System;
using System.Collections.Generic;
using System.Threading;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Client.Observability
{
    internal class RpcCallMetricsBuilder
    {
        private readonly string rpcCallName;
        private readonly DateTimeOffset start;
        private readonly TimeSpan retryTimeout;
        private readonly bool withRetries;
        private readonly List<TimedOperation> attempts = new();

        private Exception? exception;
        private bool wasCancelled;

        private RpcCallMetricsBuilder(string rpcCallName, DateTimeOffset start, bool withRetries, TimeSpan retryTimeout)
        {
            this.rpcCallName = rpcCallName;
            this.start = start;
            this.retryTimeout = retryTimeout;
            this.withRetries = withRetries;
        }

        public static RpcCallMetricsBuilder StartWithRetries(string rpcCallName, TimeSpan retryTimeout)
        {
            var start = DateTimeOffset.UtcNow;

            var builder = new RpcCallMetricsBuilder(rpcCallName, start, true, retryTimeout);
            return builder;
        }

        public static RpcCallMetricsBuilder StartWithoutRetries(string rpcCallName)
        {
            var start = DateTimeOffset.UtcNow;

            var builder = new RpcCallMetricsBuilder(rpcCallName, start, false, TimeSpan.Zero);
            return builder;
        }

        public RpcCallMetricsBuilder WithAttempt(TimedOperation attempt)
        {
            attempts.Add(attempt);

            return this;
        }

        public RpcCallMetricsBuilder Failure(Exception exception, CancellationToken cancellationToken)
        {
            this.exception = exception;
            wasCancelled = cancellationToken.IsCancellationRequested;

            return this;
        }

        public RpcCallMetrics Build()
        {
            var end = DateTimeOffset.UtcNow;

            var rpcCallMetrics = new RpcCallMetrics(
                rpcCallName,
                start,
                end,
                withRetries,
                retryTimeout,
                exception,
                wasCancelled,
                attempts);
            return rpcCallMetrics;
        }
    }
}