using System;
using System.Collections.Generic;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.CommonTestUtils
{
    public class TestRpcCallObserver : IRpcCallObserver
    {
        private readonly List<RpcCallMetrics> metrics = new();

        public IReadOnlyList<RpcCallMetrics> Metrics => metrics;

        public void RpcCallCompleted(RpcCallMetrics rpcCallMetrics)
        {
            metrics.Add(rpcCallMetrics);
        }
    }
}