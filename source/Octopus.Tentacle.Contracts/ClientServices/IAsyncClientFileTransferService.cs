using System;
using System.Threading.Tasks;
using Halibut;
using Halibut.ServiceModel;

namespace Octopus.Tentacle.Contracts.ClientServices
{
    public interface IAsyncClientFileTransferService
    {
        Task<UploadResult> UploadFileAsync(string remotePath, DataStream upload, HalibutProxyRequestOptions halibutProxyRequestOptions);
        Task<DataStream> DownloadFileAsync(string remotePath, HalibutProxyRequestOptions halibutProxyRequestOptions);
    }
}