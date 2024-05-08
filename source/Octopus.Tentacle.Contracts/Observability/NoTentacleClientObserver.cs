namespace Octopus.Tentacle.Contracts.Observability
{
    public class NoTentacleClientObserver : ITentacleClientObserver
    {
        public void RpcCallCompleted(RpcCallMetrics rpcCallMetrics, ISomethingLog logger)
        {
        }

        public void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics, ISomethingLog logger)
        {
        }

        public void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics, ISomethingLog logger)
        {
        }

        public void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics, ISomethingLog logger)
        {
        }
    }
}