using System;
using System.Linq;
using System.Collections.Generic;

namespace Octopus.Tentacle.Client.Scripts.Observability
{
    public class RpcCallMetrics
    {
        public DateTimeOffset Start { get; }
        public DateTimeOffset End { get; }
        public TimeSpan RetryTimeout { get; }
        // An exception that was not raised while retrying.
        //TODO: Is this going to contain cancellation exceptions.
        public Exception? NonRpcException { get; }
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
        public bool HasException => NonRpcException is not null;
        public bool AttemptsSucceeded => Attempts.Any() && Attempts.Last().Succeeded;


        public RpcCallMetrics(
            DateTimeOffset start,
            DateTimeOffset end,
            TimeSpan retryTimeout,
            Exception? nonRpcException,
            IReadOnlyList<TimedOperation> attempts)
        {
            Start = start;
            End = end;
            RetryTimeout = retryTimeout;
            NonRpcException = nonRpcException;
            Attempts = attempts;
        }
    }
}