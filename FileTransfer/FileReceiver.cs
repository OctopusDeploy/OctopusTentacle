using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Octopus.Platform.Deployment.Configuration;
using Octopus.Platform.Deployment.Messages.FileTransfer;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Util;
using Pipefish;
using Pipefish.Supervision;

namespace Octopus.Shared.FileTransfer
{
    [Description("Receive File")]
    public class FileReceiver : PersistentActor<FileReceiveData>,
                                ICreatedBy<BeginFileTransferCommand>,
                                IReceiveAsync<SendNextChunkReply>,
                                IReceive<ChunkAlreadySentAcknowledgement>
    {
        readonly IFileStorageConfiguration fileStorageConfiguration;
        readonly IOctopusFileSystem fileSystem;
        readonly ISupervised supervised;

        public FileReceiver(
            IFileStorageConfiguration fileStorageConfiguration,
            IOctopusFileSystem fileSystem)
        {
            this.fileStorageConfiguration = fileStorageConfiguration;
            this.fileSystem = fileSystem;
            supervised = RegisterAspect(new Supervised(config=>
                config.OnProcessTimeout(() => Log.Octopus().ErrorFormat("Transfer of {0} did not complete before the process timeout", Data.LocalPath))));
        }

        public void Receive(BeginFileTransferCommand message)
        {
            var path = Path.Combine(
                fileStorageConfiguration.FileStorageDirectory,
                message.Filename + "-" + Guid.NewGuid());

            Data = new FileReceiveData
            {
                LocalPath = path,
                Hash = message.Hash
            };
            
            Log.Octopus().InfoFormat("Beginning transfer of {0}", Data.LocalPath);
            supervised.Notify(new SendNextChunkRequest { SupportsEagerTransfer = true });
        }

        public async Task ReceiveAsync(SendNextChunkReply message)
        {
            using (var file = fileSystem.OpenFile(Data.LocalPath, FileMode.OpenOrCreate))
            {
                file.Seek(0, SeekOrigin.End);
                file.Write(message.Data, 0, message.Data.Length);
            }

            if (message.IsLastChunk)
            {
                using (var file = fileSystem.OpenFile(Data.LocalPath, FileAccess.Read))
                {
                    var hash = HashCalculator.Hash(file);
                    if (hash != Data.Hash)
                    {
                        file.Dispose();
                        fileSystem.DeleteFile(Data.LocalPath, DeletionOptions.TryThreeTimesIgnoreFailure);
                        supervised.Fail(string.Format("The file corrupted during transfer. Expected hash: {0}, got hash: {1}", Data.Hash, hash));
                        return;
                    }
                }

                supervised.Succeed(new FileTransferCompleteEvent(Data.LocalPath));
                return;
            }

            supervised.Notify(new SendNextChunkRequest { SupportsEagerTransfer = true });
        }

        public void Receive(ChunkAlreadySentAcknowledgement message)
        {
            // Just to satisfy the conversation tracker
        }
    }
}
