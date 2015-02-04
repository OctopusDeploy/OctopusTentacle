using System;
using Halibut;

namespace Octopus.Shared.Contracts
{
    public interface IFileTransferService
    {
        void UploadFile(string remotePath, DataStream stream);
        DataStream DownloadFile(string remotePath);
    }
}