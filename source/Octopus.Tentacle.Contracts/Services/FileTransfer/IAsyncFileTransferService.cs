using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;

namespace Octopus.Tentacle.Contracts.Services.FileTransfer
{
    public interface IAsyncFileTransferService
    {
        Task<UploadResult> UploadFileAsync(string remotePath, DataStream upload, CancellationToken cancellationToken);
        Task<DataStream> DownloadFileAsync(string remotePath, CancellationToken cancellationToken);
    }
}