using System;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Contracts.Observability;
using Polly.Timeout;

namespace Octopus.Tentacle.Client.Execution
{
    internal static class RpcCallExecutorFactory
    {
        internal static RpcCallExecutor Create(TimeSpan retryDuration, ITentacleObserver tentacleObserver)
        {
            var rpcCallRetryHandler = new RpcCallRetryHandler(retryDuration, TimeoutStrategy.Pessimistic);
            var rpcCallExecutor = new RpcCallExecutor(rpcCallRetryHandler, tentacleObserver);

            return rpcCallExecutor;
        }
    }
}