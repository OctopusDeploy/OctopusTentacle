using System;
using System.IO;
using System.Threading.Tasks;
using Octopus.Shared.Configuration;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Orchestration.Completion;
using Octopus.Shared.Orchestration.FileTransfer.Implementation;
using Octopus.Shared.Util;
using Pipefish;
using Pipefish.Toolkit.Origination;

namespace Octopus.Shared.Orchestration.FileTransfer
{
    public class FileReceiver : PersistentActor<FileReceiveData>,
                                ICreatedBy<BeginFileTransferCommand>,
                                IReceiveAsync<SendNextChunkReply>
    {
        readonly IFileStorageConfiguration fileStorageConfiguration;
        readonly IOctopusFileSystem fileSystem;

        public FileReceiver(
            IFileStorageConfiguration fileStorageConfiguration,
            IOctopusFileSystem fileSystem)
        {
            this.fileStorageConfiguration = fileStorageConfiguration;
            this.fileSystem = fileSystem;
            RegisterAspect(new CompletesOnTimeout(() => Log.Octopus().ErrorFormat("Transfer of {0} did not complete before the process timeout", Data.LocalPath)));
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
            Reply(message, new SendNextChunkRequest());
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
                        Reply(message, new FileTransferCompleteEvent(false, "The file corrupted during transfer", null));
                        Complete();
                        return;
                    }
                }

                Reply(message, new FileTransferCompleteEvent(true, "The file was transferred successfully", Data.LocalPath));
                Complete();
                return;
            }

            Reply(message, new SendNextChunkRequest());
        }
    }
}

