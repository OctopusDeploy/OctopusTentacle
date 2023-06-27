using System;
using System.Collections.Generic;
using System.Threading;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Client.Observability
{
    internal class ClientOperationMetricsBuilder
    {
        private readonly DateTimeOffset start;
        private readonly List<RpcCallMetrics> rpcCalls = new();

        private Exception? exception;
        private bool wasCancelled;

        public ClientOperationMetricsBuilder(DateTimeOffset start)
        {
            this.start = start;
        }

        public static ClientOperationMetricsBuilder Start()
        {
            var start = DateTimeOffset.UtcNow;

            var builder = new ClientOperationMetricsBuilder(start);
            return builder;
        }

        public ClientOperationMetrics Build()
        {
            var end = DateTimeOffset.UtcNow;

            return new ClientOperationMetrics(start, end, exception, wasCancelled, rpcCalls);
        }

        public ClientOperationMetricsBuilder WithRpcCall(RpcCallMetrics rpcCallMetrics)
        {
            rpcCalls.Add(rpcCallMetrics);
            return this;
        }

        public ClientOperationMetricsBuilder Failure(Exception exception, CancellationToken cancellationToken)
        {
            this.exception = exception;
            wasCancelled = cancellationToken.IsCancellationRequested;

            return this;
        }
    }
}