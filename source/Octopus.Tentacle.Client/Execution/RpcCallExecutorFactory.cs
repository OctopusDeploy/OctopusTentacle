using System;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Contracts.Observability;
using Polly.Timeout;

namespace Octopus.Tentacle.Client.Execution
{
    internal static class RpcCallExecutorFactory
    {
        internal static RpcCallExecutor Create(TimeSpan retryDuration, ITentacleClientObserver tentacleClientObserver, bool useTaskCreationOptionsLongRunning)
        {
            var rpcCallRetryHandler = new RpcCallRetryHandler(retryDuration, TimeoutStrategy.Pessimistic);
            var rpcCallExecutor = new RpcCallExecutor(rpcCallRetryHandler, tentacleClientObserver, useTaskCreationOptionsLongRunning);

            return rpcCallExecutor;
        }
    }
}