using System;
using Octopus.Tentacle.Contracts.Logging;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Client.Observability
{
    public class NonThrowingTentacleClientObserverDecorator : ITentacleClientObserver
    {
        private readonly ITentacleClientObserver inner;

        public NonThrowingTentacleClientObserverDecorator(ITentacleClientObserver inner)
        {
            this.inner = inner;
        }

        public void RpcCallCompleted(RpcCallMetrics rpcCallMetrics, ITaskLog logger)
        {
            try
            {
                inner.RpcCallCompleted(rpcCallMetrics, logger);
            }
            catch (Exception e)
            {
                logger.Warn(e, "An error occurred while notifying the Tentacle Client Observer of the RPC call completion.");
            }
        }

        public void UploadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITaskLog logger)
        {
            try
            {
                inner.UploadFileCompleted(clientOperationMetrics, logger);
            }
            catch (Exception e)
            {
                logger.Warn(e, "An error occurred while notifying the Tentacle Client Observer of the Upload File completion.");
            }
        }

        public void DownloadFileCompleted(ClientOperationMetrics clientOperationMetrics, ITaskLog logger)
        {
            try
            {
                inner.DownloadFileCompleted(clientOperationMetrics, logger);
            }
            catch (Exception e)
            {
                logger.Warn(e, "An error occurred while notifying the Tentacle Client Observer of the Download File completion.");
            }
        }

        public void ExecuteScriptCompleted(ClientOperationMetrics clientOperationMetrics, ITaskLog logger)
        {
            try
            {
                inner.ExecuteScriptCompleted(clientOperationMetrics, logger);
            }
            catch (Exception e)
            {
                logger.Warn(e, "An error occurred while notifying the Tentacle Client Observer of the Execute Script completion.");
            }
        }
    }
}