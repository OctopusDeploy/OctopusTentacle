using Octopus.Tentacle.Contracts.Logging;

namespace Octopus.Tentacle.Contracts.Observability
{
    public class NoTentacleClientObserver : ITentacleClientObserver
    {
        public void RpcCallCompleted(RpcCallMetrics rpcCallMetrics, IOperationLog logger)
        {
        }

        public void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics, IOperationLog logger)
        {
        }

        public void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics, IOperationLog logger)
        {
        }

        public void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics, IOperationLog logger)
        {
        }
    }
}