using Octopus.Diagnostics;

namespace Octopus.Tentacle.Contracts.Observability
{
    public class NoTentacleClientObserver : ITentacleClientObserver
    {
        public void RpcCallCompleted(RpcCallMetrics rpcCallMetrics, ILog logger)
        {
        }

        public void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics, ILog logger)
        {
        }

        public void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics, ILog logger)
        {
        }

        public void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics, ILog logger)
        {
        }
    }
}