using System;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.SyncAndAsyncProxies
{
    internal class ClientFileTransferServiceAsyncToSyncProxy : IClientFileTransferService
    {
        private readonly IAsyncClientFileTransferService service;

        public ClientFileTransferServiceAsyncToSyncProxy(IAsyncClientFileTransferService service)
        {
            this.service = service;
        }

        public UploadResult UploadFile(string remotePath, DataStream upload, HalibutProxyRequestOptions proxyRequestOptions)
        {
            return service.UploadFileAsync(remotePath, upload, proxyRequestOptions).GetAwaiter().GetResult();
        }

        public DataStream DownloadFile(string remotePath, HalibutProxyRequestOptions proxyRequestOptions)
        {
            return service.DownloadFileAsync(remotePath, proxyRequestOptions).GetAwaiter().GetResult();
        }
    }
}