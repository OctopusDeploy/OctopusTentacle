using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Services.FileTransfer
{
    [Service(typeof(IFileTransferService))]
    public class FileTransferService : IAsyncFileTransferService
    {
        readonly ISystemLog log;
        readonly IOctopusFileSystem fileSystem;
        readonly IHomeConfiguration home;

        public FileTransferService(IOctopusFileSystem fileSystem, IHomeConfiguration home, ISystemLog log)
        {
            this.fileSystem = fileSystem;
            this.home = home;
            this.log = log;
        }

        public async Task<DataStream> DownloadFileAsync(string remotePath, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            var fullPath = ResolvePath(remotePath);
            if (!fileSystem.FileExists(fullPath))
            {
                log.Trace("Client requested a file download, but the file does not exist: " + fullPath);
                return null!;
            }

            var fileSize = fileSystem.GetFileSize(fullPath);

            // Check we can open the file, because if this throws in the
            // DataStream lambda below it causes Halibut issues. Yes there
            // is a race condition doing it here. We could open here and
            // close in the DataStream lambda, but that risks leaving
            // the file open if the lambda isn't executed.
            using (fileSystem.OpenFile(fullPath, FileAccess.Read, FileShare.ReadWrite))
            {}


            return new DataStream(fileSize, async (writer, ct) =>
            {
                log.Trace("Begin streaming file download: " + fullPath);
                using (var stream = fileSystem.OpenFile(fullPath, FileAccess.Read, FileShare.ReadWrite))
                {
                    await stream.CopyToAsync(writer, 81920, ct);
                    await writer.FlushAsync(ct);
                    log.Trace("Finished streaming file download: " + fullPath);
                }
            });
        }

        public async Task<UploadResult> UploadFileAsync(string remotePath, DataStream upload, CancellationToken cancellationToken)
        {
            if (upload == null!)
            {
                log.Trace("Client requested a file upload, but no content stream was provided.");
                return new UploadResult(ResolvePath(remotePath), null!, 0);
            }

            var fullPath = ResolvePath(remotePath);
            var parentDirectory = Path.GetDirectoryName(fullPath);
            if (parentDirectory == null)
                throw new InvalidOperationException($"Unable to determine parent directory from path {fullPath}");
            fileSystem.EnsureDirectoryExists(parentDirectory);
            fileSystem.EnsureDiskHasEnoughFreeSpace(parentDirectory, upload.Length);

            log.Trace("Copying uploaded data stream to: " + fullPath);
            await upload.Receiver().SaveToAsync(fullPath, CancellationToken.None);
            return new UploadResult(fullPath, HashFile(fullPath), fileSystem.GetFileSize(fullPath));
        }

        string HashFile(string fullPath)
        {
            string hash;
            using (var destinationStream = fileSystem.OpenFile(fullPath, FileAccess.Read, FileShare.Read))
            {
                hash = HashCalculator.Hash(destinationStream);
            }
            return hash;
        }

        string ResolvePath(string path)
        {
            if (!PlatformDetection.IsRunningOnWindows)
            {
                path = path.Replace('\\', Path.DirectorySeparatorChar);
            }

            if (Path.IsPathRooted(path))
                return fileSystem.GetFullPath(path);

            if (home.HomeDirectory == null)
                throw new InvalidOperationException("The Tentacle home directory has not been set.");
            return fileSystem.GetFullPath(Path.Combine(home.HomeDirectory, path));
        }
    }
}