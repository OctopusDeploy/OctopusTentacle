using System;
using System.Collections.Generic;
using System.Threading;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Client.Observability
{
    internal class RpcCallMetricsBuilder
    {
        private readonly RpcCall rpcCall;
        private readonly DateTimeOffset start;
        private readonly TimeSpan retryTimeout;
        private readonly bool withRetries;
        private readonly List<TimedOperation> attempts = new();

        private Exception? exception;
        private bool wasCancelled;

        private RpcCallMetricsBuilder(RpcCall rpcCall, DateTimeOffset start, bool withRetries, TimeSpan retryTimeout)
        {
            this.rpcCall = rpcCall;
            this.start = start;
            this.retryTimeout = retryTimeout;
            this.withRetries = withRetries;
        }

        public static RpcCallMetricsBuilder StartWithRetries(RpcCall rpcCall, TimeSpan retryTimeout)
        {
            var start = DateTimeOffset.UtcNow;

            var builder = new RpcCallMetricsBuilder(rpcCall, start, true, retryTimeout);
            return builder;
        }

        public static RpcCallMetricsBuilder StartWithoutRetries(RpcCall rpcCall)
        {
            var start = DateTimeOffset.UtcNow;

            var builder = new RpcCallMetricsBuilder(rpcCall, start, false, TimeSpan.Zero);
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
                rpcCall,
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