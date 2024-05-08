using Octopus.Tentacle.Contracts.Logging;

namespace Octopus.Tentacle.Contracts.Observability
{
    public class NoTentacleClientObserver : ITentacleClientObserver
    {
        public void RpcCallCompleted(RpcCallMetrics rpcCallMetrics, ITaskLog logger)
        {
        }

        public void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITaskLog logger)
        {
        }

        public void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITaskLog logger)
        {
        }

        public void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics, ITaskLog logger)
        {
        }
    }
}