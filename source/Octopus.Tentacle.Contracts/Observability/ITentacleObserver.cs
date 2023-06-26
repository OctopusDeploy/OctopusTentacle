using System;

namespace Octopus.Tentacle.Contracts.Observability
{
    public interface ITentacleObserver
    {
        void RpcCallCompleted(RpcCallMetrics rpcCallMetrics);
        void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics);
        void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics);
        void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics);
    }
}