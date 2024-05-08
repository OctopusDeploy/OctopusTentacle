using System;

namespace Octopus.Tentacle.Contracts.Observability
{
    public interface ITentacleClientObserver
    {
        void RpcCallCompleted(RpcCallMetrics rpcCallMetrics, ITentacleContractLogger logger);
        void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleContractLogger logger);
        void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleContractLogger logger);
        void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleContractLogger logger);
    }
}