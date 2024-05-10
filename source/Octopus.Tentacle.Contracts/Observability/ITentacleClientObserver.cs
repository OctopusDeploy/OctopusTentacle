using System;
using Octopus.Tentacle.Contracts.Logging;

namespace Octopus.Tentacle.Contracts.Observability
{
    public interface ITentacleClientObserver
    {
        void RpcCallCompleted(RpcCallMetrics rpcCallMetrics, ITentacleClientTaskLog logger);
        void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleClientTaskLog logger);
        void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleClientTaskLog logger);
        void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleClientTaskLog logger);
    }
}