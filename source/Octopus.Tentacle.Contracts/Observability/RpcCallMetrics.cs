using System;
using System.Collections.Generic;
using System.Linq;

namespace Octopus.Tentacle.Contracts.Observability
{
    public class RpcCallMetrics
    {
        public string RpcCallName { get; }
        public DateTimeOffset Start { get; }
        public DateTimeOffset End { get; }
        public bool WithRetries { get; }
        public TimeSpan RetryTimeout { get; }
        public Exception? Exception { get; }
        public bool WasCancelled { get; }
        public IReadOnlyList<TimedOperation> Attempts { get; }

        public TimeSpan Duration => End - Start;

        public TimeSpan AdditionalTimeFromRetries
        {
            get
            {
                if (Attempts.Count <= 1) return TimeSpan.Zero;
                
                if (AttemptsSucceeded)
                {
                    // The 'successful' attempt is the true duration, and everything before that is the additional time from retries.
                    var successfulAdditionalTime = Attempts
                        .Take(Attempts.Count - 1)
                        .Sum(a => a.Duration.Ticks);
                    return TimeSpan.FromTicks(successfulAdditionalTime);
                }

                // If we failed, then the first failure is the true duration, and everything afterwards is additional time due to retries
                var failedAdditionalTime = Attempts
                    .Skip(1)
                    .Sum(a => a.Duration.Ticks);
                return TimeSpan.FromTicks(failedAdditionalTime);
            }
        }

        public bool Succeeded => !HasException && AttemptsSucceeded;
        public bool HasException => Exception is not null;
        public bool AttemptsSucceeded => Attempts.Any() && Attempts.Last().Succeeded;


        public RpcCallMetrics(
            string rpcCallName,
            DateTimeOffset start,
            DateTimeOffset end,
            bool withRetries,
            TimeSpan retryTimeout,
            Exception? exception,
            bool wasCancelled,
            IReadOnlyList<TimedOperation> attempts)
        {
            RpcCallName = rpcCallName;
            Start = start;
            End = end;
            WithRetries = withRetries;
            RetryTimeout = retryTimeout;
            Exception = exception;
            WasCancelled = wasCancelled;
            Attempts = attempts;
        }
    }
}