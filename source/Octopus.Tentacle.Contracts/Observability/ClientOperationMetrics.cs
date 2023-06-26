using System;
using System.Collections.Generic;

namespace Octopus.Tentacle.Contracts.Observability
{
    public class ClientOperationMetrics
    {
        public DateTimeOffset Start { get; }
        public DateTimeOffset End { get; }
        public Exception? Exception { get; }
        public bool WasCancelled { get; }
        public IReadOnlyList<RpcCallMetrics> RpcCalls { get; }

        public TimeSpan Duration => End - Start;
        public bool Succeeded => Exception is null;
        
        public ClientOperationMetrics(
            DateTimeOffset start,
            DateTimeOffset end,
            Exception? exception,
            bool wasCancelled,
            IReadOnlyList<RpcCallMetrics> rpcCalls)
        {
            Start = start;
            End = end;
            Exception = exception;
            WasCancelled = wasCancelled;
            RpcCalls = rpcCalls;
        }
    }
}