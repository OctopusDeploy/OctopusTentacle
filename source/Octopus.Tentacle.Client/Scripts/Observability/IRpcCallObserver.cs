namespace Octopus.Tentacle.Client.Scripts.Observability
{
    public interface IRpcCallObserver
    {
        void RpcCallCompleted(RpcCallMetrics rpcCallMetrics);
        void DownloadFileCompleted(TimedOperation timedOperation);
        void UploadFileCompleted(TimedOperation timedOperation);
        void ExecuteScriptCompleted(TimedOperation timedOperation);
    }
}