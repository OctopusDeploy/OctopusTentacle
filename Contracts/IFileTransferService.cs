using System;
using Halibut;

namespace Octopus.Shared.Contracts
{
    public interface IFileTransferService
    {
        void UploadFile(string remotePath, DataStream upload);
        DataStream DownloadFile(string remotePath);
    }
}