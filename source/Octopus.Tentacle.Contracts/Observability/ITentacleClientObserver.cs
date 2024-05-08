using System;
using Octopus.Tentacle.Contracts.Logging;

namespace Octopus.Tentacle.Contracts.Observability
{
    public interface ITentacleClientObserver
    {
        void RpcCallCompleted(RpcCallMetrics rpcCallMetrics, ITaskLog logger);
        void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITaskLog logger);
        void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITaskLog logger);
        void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics, ITaskLog logger);
    }
}