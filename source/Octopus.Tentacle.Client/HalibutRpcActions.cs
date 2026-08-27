using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.Observability;
using Octopus.Tentacle.Client.Scripts;
using Octopus.Tentacle.Client.ServiceHelpers;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Logging;
using Octopus.Tentacle.Contracts.Observability;

namespace Octopus.Tentacle.Client
{
    public class HalibutRpcActions : IRpcActions
    {
        readonly AllClients allClients;

        internal HalibutRpcActions(ServiceEndPoint serviceEndPoint, IHalibutRuntime halibutRuntime, ITentacleServiceDecoratorFactory? tentacleServicesDecoratorFactory)
        {
            if (halibutRuntime.OverrideErrorResponseMessageCaching == null)
            {
                // Best effort to make sure the HalibutRuntime has been configured to Cache ServiceNotFoundExceptions
                // Do not configure the HalibutRuntime here as it should only be done once and configuring it here will result in it being performed a lot
                throw new ArgumentException("Ensure that TentacleClient.CacheServiceWasNotFoundResponseMessages has been called for the HalibutRuntime", nameof(halibutRuntime));
            }
            
            allClients = new AllClients(halibutRuntime, serviceEndPoint, tentacleServicesDecoratorFactory);
        }
        
        public async Task<DataStream> DownloadFile(string remotePath, CancellationToken cancellationToken)
        {
            return await allClients.ClientFileTransferServiceV1.DownloadFileAsync(remotePath, new HalibutProxyRequestOptions(cancellationToken));
        }

        public async Task<UploadResult> UploadFile(string path, DataStream package, CancellationToken cancellationToken)
        {
            return await allClients.ClientFileTransferServiceV1.UploadFileAsync(path, package, new HalibutProxyRequestOptions(cancellationToken));
        }

        public IScriptExecutor CreateScriptExecutor(ITentacleClientTaskLog logger,
            ITentacleClientObserver tentacleClientObserver,
            IClientOperationMetricsBuilder operationMetricsBuilder,
            TentacleClientOptions clientOptions,
            TimeSpan onCancellationAbandonCompleteScriptAfter )
        {
            return new HalibutScriptExecutor(
                allClients,
                logger, 
                tentacleClientObserver,
                operationMetricsBuilder,
                clientOptions,
                onCancellationAbandonCompleteScriptAfter);
        }
    }
}