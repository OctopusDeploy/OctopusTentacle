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