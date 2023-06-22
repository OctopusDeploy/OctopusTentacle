namespace Octopus.Tentacle.Client.Scripts.Observability
{
    public interface IRpcCallObserver
    {
        void RpcCallCompleted(RpcCallMetrics rpcCallMetrics);
    }
}