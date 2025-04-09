using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut;

<<<<<<<< HEAD:source/Octopus.Tentacle.Contracts/Services/FileTransfer/IAsyncFileTransferService.cs
namespace Octopus.Tentacle.Contracts.Services.FileTransfer
========
// Don't "fix" this namespace, this is what we originally did and perhaps
// what we need to keep doing.
namespace Octopus.Tentacle.Services.FileTransfer
>>>>>>>> a3cc9421 (Move script iso mutex):source/Octopus.Tentacle.Contracts/Services/FileTransferService/IAsyncFileTransferService.cs
{
    public interface IAsyncFileTransferService
    {
        Task<UploadResult> UploadFileAsync(string remotePath, DataStream upload, CancellationToken cancellationToken);
        Task<DataStream> DownloadFileAsync(string remotePath, CancellationToken cancellationToken);
    }
}