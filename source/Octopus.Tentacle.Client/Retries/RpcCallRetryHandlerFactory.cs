using System;
using Octopus.Tentacle.Contracts.Observability;
using Polly.Timeout;

namespace Octopus.Tentacle.Client.Retries
{
    internal static class RpcCallRetryHandlerFactory
    {
        internal static IRpcCallRetryHandler Create(TimeSpan retryDuration, IRpcCallObserver? rpcCallObserver)
        {
            var rpcCallRetryHandler = new RpcCallRetryHandler(retryDuration, TimeoutStrategy.Pessimistic);
            
            if (rpcCallObserver is not null)
            {
                var rpcCallRetryHandlerObservabilityDecorator = new RpcCallRetryHandlerObservabilityDecorator(rpcCallRetryHandler, rpcCallObserver);
                return rpcCallRetryHandlerObservabilityDecorator;
            }

            return rpcCallRetryHandler;
        }
    }
}