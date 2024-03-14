using Octopus.Tentacle.Client.Execution;
using Octopus.Tentacle.Client.Retries;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Client.Tests.Builders
{
    class RpcCallExecutorBuilder
    {
        RpcCallRetryHandler? rpcCallRetryHandler;
        ITentacleClientObserver? tentacleClientObserver;

        public RpcCallExecutorBuilder WithRpcCallRetryHandler(RpcCallRetryHandler rpcCallRetryHandler)
        {
            this.rpcCallRetryHandler = rpcCallRetryHandler;
            return this;
        }

        public RpcCallExecutorBuilder WithTentacleClientObserver(ITentacleClientObserver tentacleClientObserver)
        {
            this.tentacleClientObserver = tentacleClientObserver;
            return this;
        }

        public RpcCallExecutor Build() =>
            new RpcCallExecutor(
                rpcCallRetryHandler ?? RpcCallRetryHandlerBuilder.Default(),
                tentacleClientObserver ?? TentacleClientObserverBuilder.Default());

        public static RpcCallExecutor Default() => new RpcCallExecutorBuilder().Build();
    }
}