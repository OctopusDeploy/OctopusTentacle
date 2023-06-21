using System;
using System.Collections.Generic;
using System.Threading;

namespace Octopus.Tentacle.Client.Scripts.Observability
{
    public class RpcCallMetricsBuilder
    {
        private readonly DateTimeOffset start;
        private readonly TimeSpan retryTimeout;
        private readonly List<TimedOperation> attempts = new();

        private DateTimeOffset? attemptStart;
        private Exception? exception;

        private RpcCallMetricsBuilder(DateTimeOffset start, TimeSpan retryTimeout)
        {
            this.start = start;
            this.retryTimeout = retryTimeout;
        }

        public static RpcCallMetricsBuilder Start(TimeSpan retryTimeout)
        {
            var start = DateTimeOffset.UtcNow;
            var builder = new RpcCallMetricsBuilder(start, retryTimeout);
            return builder;
        }

        public RpcCallMetricsBuilder StartAttempt()
        {
            attemptStart = DateTimeOffset.UtcNow;
            return this;
        }

        public RpcCallMetricsBuilder AttemptSuccessful()
        {
            if (attemptStart is null) throw new InvalidOperationException("Attempt has not been started.");
            
            var attempt = TimedOperation.Success(attemptStart.Value);
            attempts.Add(attempt);

            attemptStart = null;
            return this;
        }

        public RpcCallMetricsBuilder AttemptFailed(Exception attemptException, CancellationToken cancellationToken)
        {
            if (attemptStart is null) throw new InvalidOperationException("Attempt has not been started.");
            
            var attempt = TimedOperation.Failure(attemptStart.Value, attemptException);
            attempts.Add(attempt);

            attemptStart = null;
            return this;
        }

        public RpcCallMetricsBuilder Failure(Exception exception, Canc)
        {
            this.exception = exception;
            return this;
        }

        public RpcCallMetrics Build()
        {
            if (attemptStart is not null) throw new InvalidOperationException("Building RPC call metrics with attempt in progress.");

            var end = DateTimeOffset.UtcNow;
            var rpcCallMetrics = new RpcCallMetrics(start, end, retryTimeout, exception, attempts);
            return rpcCallMetrics;
        }
    }
}