namespace Octopus.Tentacle.Contracts.Observability
{
    public class NoTentacleClientObserver : ITentacleClientObserver
    {
        public void RpcCallCompleted(RpcCallMetrics rpcCallMetrics, ITentacleContractLogger logger)
        {
        }

        public void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleContractLogger logger)
        {
        }

        public void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleContractLogger logger)
        {
        }

        public void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics, ITentacleContractLogger logger)
        {
        }
    }
}