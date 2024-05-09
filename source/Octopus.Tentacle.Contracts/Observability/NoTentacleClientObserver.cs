using Octopus.Tentacle.Contracts.Logging;

namespace Octopus.Tentacle.Contracts.Observability
{
    public class NoTentacleClientObserver : ITentacleClientObserver
    {
        public void RpcCallCompleted(RpcCallMetrics rpcCallMetrics, ITentacleTaskLog logger)
        {
        }

        public void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleTaskLog logger)
        {
        }

        public void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleTaskLog logger)
        {
        }

        public void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleTaskLog logger)
        {
        }
    }
}