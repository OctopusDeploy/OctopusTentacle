using System;
using System.Threading;

namespace Octopus.Tentacle.Client.Scripts.Observability
{
    public class TimedOperation
    {
        public DateTimeOffset Start { get; }
        public DateTimeOffset End { get; }
        public Exception? Exception { get; }
        public bool WasCancelled { get; }
        public TimeSpan Duration => End - Start;

        public bool Succeeded => Exception is null;

        private TimedOperation(DateTimeOffset start, DateTimeOffset end, Exception? exception, bool wasCancelled)
        {
            Start = start;
            End = end;
            Exception = exception;
            WasCancelled = wasCancelled;
        }

        public static TimedOperation Success(DateTimeOffset start)
        {
            var end = DateTimeOffset.UtcNow;

            var rpcCallAttempt = new TimedOperation(start, end, null, false);
            return rpcCallAttempt;
        }

        public static TimedOperation Failure(DateTimeOffset start, Exception exception, CancellationToken cancellationToken)
        {
            var end = DateTimeOffset.UtcNow;

            var rpcCallAttempt = new TimedOperation(start, end, exception, cancellationToken.IsCancellationRequested);
            return rpcCallAttempt;
        }
    }
}