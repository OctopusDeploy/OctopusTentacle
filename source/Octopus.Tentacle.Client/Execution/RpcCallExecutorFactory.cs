using System;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Client.Execution
{
    internal static class RpcCallExecutorFactory
    {
        internal static RpcCallExecutor Create(TimeSpan retryDuration, ITentacleClientObserver tentacleClientObserver, int? minimumAttemptsForInterruptedLongRunningCalls = null)
        {
            var rpcCallRetryHandler = new RpcCallRetryHandler(retryDuration, minimumAttemptsForInterruptedLongRunningCalls);
            var rpcCallExecutor = new RpcCallExecutor(rpcCallRetryHandler, tentacleClientObserver);

            return rpcCallExecutor;
        }
    }
}
