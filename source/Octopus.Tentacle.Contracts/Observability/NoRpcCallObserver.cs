namespace Octopus.Tentacle.Contracts.Observability
{
    public class NoRpcCallObserver : IRpcCallObserver
    {
        public void RpcCallCompleted(RpcCallMetrics rpcCallMetrics)
        {
        }
    }
}