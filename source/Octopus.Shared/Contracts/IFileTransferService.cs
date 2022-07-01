using System;
using Halibut;

namespace Octopus.Shared.Contracts
{
    public interface IFileTransferService
    {
        UploadResult UploadFile(string remotePath, DataStream upload);
        DataStream DownloadFile(string remotePath);
    }
}