using System;

namespace Octopus.Tentacle.Contracts.Observability
{
    public interface ITentacleClientObserver
    {
        void RpcCallCompleted(RpcCallMetrics rpcCallMetrics);
        void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics);
        void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics);
        void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics);
    }
}