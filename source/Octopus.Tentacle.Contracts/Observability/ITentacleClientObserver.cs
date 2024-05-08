using System;
using Octopus.Tentacle.Contracts.Logging;

namespace Octopus.Tentacle.Contracts.Observability
{
    public interface ITentacleClientObserver
    {
        void RpcCallCompleted(RpcCallMetrics rpcCallMetrics, IOperationLog logger);
        void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics, IOperationLog logger);
        void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics, IOperationLog logger);
        void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics, IOperationLog logger);
    }
}