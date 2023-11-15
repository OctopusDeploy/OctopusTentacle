using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Octopus.Tentacle.Contracts;

namespace Octopus.Tentacle.Services.FileTransfer
{
    public interface IAsyncFileTransferService
    {
        Task<UploadResult> UploadFileAsync(string remotePath, DataStream upload, CancellationToken cancellationToken);
        Task<DataStream> DownloadFileAsync(string remotePath, CancellationToken cancellationToken);
    }
}