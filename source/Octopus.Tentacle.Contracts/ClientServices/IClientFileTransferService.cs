using Halibut;
using Halibut.ServiceModel;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Client.ClientServices
{
    public interface IClientFileTransferService
    {
        UploadResult UploadFile(string remotePath, DataStream upload, HalibutProxyRequestOptions proxyRequestOptions);
        DataStream DownloadFile(string remotePath, HalibutProxyRequestOptions proxyRequestOptions);
    }
}