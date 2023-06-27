namespace Octopus.Tentacle.Contracts.Observability
{
    public class NoTentacleClientObserver : ITentacleClientObserver
    {
        public void RpcCallCompleted(RpcCallMetrics rpcCallMetrics)
        {
        }

        public void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics)
        {
        }

        public void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics)
        {
        }

        public void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics)
        {
        }
    }
}