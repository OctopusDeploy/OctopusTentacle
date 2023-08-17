using System;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Contracts.Observability
{
    public interface ITentacleClientObserver
    {
        void RpcCallCompleted(RpcCallMetrics rpcCallMetrics, ILog logger);
        void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics, ILog logger);
        void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics, ILog logger);
        void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics, ILog logger);
    }
}