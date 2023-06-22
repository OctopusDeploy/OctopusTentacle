using System;

namespace Octopus.Tentacle.Contracts.Observability
{
    public interface IRpcCallObserver
    {
        void RpcCallCompleted(RpcCallMetrics rpcCallMetrics);
    }
}