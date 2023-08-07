using System;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Client.ClientServices;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ClientServices;

namespace Octopus.Tentacle.Tests.Integration.Util.Builders.Decorators.SyncAndAsyncProxies
{
    internal class ClientFileTransferServiceSyncToAsyncProxy : IAsyncClientFileTransferService
    {
        private readonly IClientFileTransferService service;

        public ClientFileTransferServiceSyncToAsyncProxy(IClientFileTransferService service)
        {
            this.service = service;
        }

        public Task<UploadResult> UploadFileAsync(string remotePath, DataStream upload, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            var result = service.UploadFile(remotePath, upload, halibutProxyRequestOptions);

            return Task.FromResult(result);
        }

        public Task<DataStream> DownloadFileAsync(string remotePath, HalibutProxyRequestOptions halibutProxyRequestOptions)
        {
            var result = service.DownloadFile(remotePath, halibutProxyRequestOptions);

            return Task.FromResult(result);
        }
    }
}