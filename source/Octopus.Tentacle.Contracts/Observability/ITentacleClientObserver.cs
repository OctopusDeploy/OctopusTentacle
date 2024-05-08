using System;

namespace Octopus.Tentacle.Contracts.Observability
{
    public interface ITentacleClientObserver
    {
        void RpcCallCompleted(RpcCallMetrics rpcCallMetrics, ISomethingLog logger);
        void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics, ISomethingLog logger);
        void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics, ISomethingLog logger);
        void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics, ISomethingLog logger);
    }
}