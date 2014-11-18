using System;
using System.IO;
using System.Threading;
using Octopus.Platform.Util;
using Pipefish.Streaming;

namespace Octopus.Shared.Communications
{
    public class StreamStore : IOctopusStreamStore
    {
        private readonly string rootDirectory;
        readonly IOctopusFileSystem fileSystem;

        public StreamStore(string rootDirectory, IOctopusFileSystem fileSystem)
        {
            this.rootDirectory = rootDirectory;
            this.fileSystem = fileSystem;

            fileSystem.EnsureDirectoryExists(rootDirectory);
        }

        public StreamReceipt Write(Stream content, long value)
        {
            var identifier = new StreamIdentifier(Guid.NewGuid().ToString());
            var path = GetPath(identifier);

            var buffer = new byte[4 * 1024 * 1024];
            var bytesRemaining = value;
            using (var fileStream = fileSystem.OpenFile(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
            {
                while (bytesRemaining > 0)
                {
                    var bytesToRead = buffer.Length;
                    if (bytesRemaining < bytesToRead)
                    {
                        bytesToRead = (int)bytesRemaining;
                    }

                    var bytesRead = content.Read(buffer, 0, bytesToRead);
                    bytesRemaining -= bytesRead;

                    fileStream.Write(buffer, 0, bytesRead);
                }

                fileStream.Flush();
            }

            return new StreamReceipt(identifier, new FileInfo(path).Length);
        }

        public void Move(StreamIdentifier streamIdentifier, string newFilePath)
        {
            var success = false;
            var path = GetPath(streamIdentifier);
            for (var i = 1; i <= 3; i++)
            {
                try
                {
                    fileSystem.MoveFile(path, newFilePath);
                    success = true;
                    break;
                }
                catch (Exception ex)
                {
                    Thread.Sleep(1000 * i * i);
                }
            }

            if (!success)
            {
                fileSystem.CopyFile(path, newFilePath);
                fileSystem.DeleteFile(path, DeletionOptions.TryThreeTimesIgnoreFailure);
            }
        }

        public void Read(StreamIdentifier streamIdentifier, Action<Stream> reader)
        {
            var path = GetPath(streamIdentifier);
            if (!fileSystem.FileExists(path))
            {
                throw new Exception("The stream " + streamIdentifier + " does not exist. It may have been deleted.");
            }

            using (var fileStream = fileSystem.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                reader(fileStream);
            }
        }

        public void Delete(StreamIdentifier streamIdentifier)
        {
            fileSystem.DeleteFile(GetPath(streamIdentifier), DeletionOptions.TryThreeTimesIgnoreFailure);
        }

        string GetPath(StreamIdentifier identifier)
        {
            return Path.Combine(rootDirectory, identifier + ".octostream");
        }
    }
}