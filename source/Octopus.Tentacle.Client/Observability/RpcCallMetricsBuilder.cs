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
        private readonly List<TimedOperation> attempts = new();

        private Exception? exception;
        private bool wasCancelled;

        private RpcCallMetricsBuilder(string rpcCallName, DateTimeOffset start, TimeSpan retryTimeout)
        {
            this.rpcCallName = rpcCallName;
            this.start = start;
            this.retryTimeout = retryTimeout;
        }

        public static RpcCallMetricsBuilder Start(string rpcCallName, TimeSpan retryTimeout)
        {
            var start = DateTimeOffset.UtcNow;

            var builder = new RpcCallMetricsBuilder(rpcCallName, start, retryTimeout);
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
                retryTimeout,
                exception,
                wasCancelled,
                attempts);
            return rpcCallMetrics;
        }
    }
}