using System;
using System.IO;
using System.Threading.Tasks;
using Octopus.Shared.Configuration;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Util;
using Pipefish;
using Pipefish.Messages;

namespace Octopus.Shared.Orchestration.FileTransfer.Implementation
{
    public class FileReceiver : PersistentActor<FileReceiveData>,
                                ICreatedBy<BeginFileTransferCommand>,
                                IReceive<TimeoutElapsedEvent>,
                                IReceiveAsync<SendNextChunkReply>
    {
        readonly IFileStorageConfiguration fileStorageConfiguration;
        readonly IOctopusFileSystem fileSystem;

        readonly TimeSpan ProcessTimeout = TimeSpan.FromDays(90);

        public FileReceiver(
            IFileStorageConfiguration fileStorageConfiguration,
            IOctopusFileSystem fileSystem)
        {
            this.fileStorageConfiguration = fileStorageConfiguration;
            this.fileSystem = fileSystem;
        }

        public void Receive(BeginFileTransferCommand message)
        {
            var path = Path.Combine(
                fileStorageConfiguration.FileStorageDirectory,
                message.Filename + "#" + message.Hash);

            if (fileSystem.FileExists(path))
            {
                Reply(message, new FileTransferCompleteEvent(true, true, "The file is already present on the machine", path));
                Complete();
                return;
            }

            Data = new FileReceiveData
            {
                LocalPath = path
            };
            
            SetTimeout(ProcessTimeout);

            Log.Octopus().InfoFormat("Beginning transfer of {0}", Data.LocalPath);
            Reply(message, new SendNextChunkRequest());
        }

        public void Receive(TimeoutElapsedEvent message)
        {
            Log.Octopus().ErrorFormat("Transfer of {0} did not complete before the process timeout", ProcessTimeout);
            Complete();
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
                Reply(message, new FileTransferCompleteEvent(true, false, "The file was transferred successfully", Data.LocalPath));
                Complete();
                return;
            }

            Reply(message, new SendNextChunkRequest());
        }
    }
}

