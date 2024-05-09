using System;
using Octopus.Tentacle.Contracts.Logging;

namespace Octopus.Tentacle.Contracts.Observability
{
    public interface ITentacleClientObserver
    {
        void RpcCallCompleted(RpcCallMetrics rpcCallMetrics, ITentacleTaskLog logger);
        void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleTaskLog logger);
        void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleTaskLog logger);
        void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleTaskLog logger);
    }
}