using System;
using System.IO;
using System.Threading.Tasks;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Util;
using Pipefish;
using Pipefish.Messages;

namespace Octopus.Shared.Orchestration.FileTransfer.Implementation
{
    public class FileSender : 
        PersistentActor<FileSendData>,
        ICreatedBy<SendFileRequest>,
        IReceiveAsync<SendNextChunkRequest>,
        IReceive<FileTransferCompleteEvent>,
        IReceive<TimeoutElapsedEvent>
    {
        readonly IOctopusFileSystem fileSystem;
        const int ChunkSize = 128 * 1024;
        readonly TimeSpan ProcessTimeout = TimeSpan.FromDays(90);

        public FileSender(IOctopusFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public void Receive(SendFileRequest message)
        {
            Data = new FileSendData { 
                LocalFilename = message.LocalFilename, 
                Hash = message.Hash, 
                NextChunkIndex = 0,
                ReplyTo = message.GetMessage().From,
                ExpectedSize = message.ExpectedSize,
            };

            SetTimeout(ProcessTimeout);

            Dispatch(message.RemoteSquid, new BeginFileTransferCommand(Path.GetFileName(Data.LocalFilename), Data.Hash, Data.ExpectedSize), null);
        }

        public async Task ReceiveAsync(SendNextChunkRequest message)
        {
            var nextChunkOffset = Data.NextChunkIndex * ChunkSize;

            using (var file = fileSystem.OpenFile(Data.LocalFilename, FileAccess.Read))
            {
                file.Seek(nextChunkOffset, SeekOrigin.Begin);
                var bytes = new byte[ChunkSize];
                var read = await file.ReadAsync(bytes, 0, ChunkSize);
                if (read != ChunkSize)
                    Array.Resize(ref bytes, read);
                var chunk = new SendNextChunkReply(bytes, read != ChunkSize);
                Reply(message, chunk, ProcessTimeout);
            }
        }

        public void Receive(FileTransferCompleteEvent message)
        {
            var remoteSpace = message.GetMessage().From.Space;

            if (message.Succeeded)
                Log.Octopus().InfoFormat("File {0} with hash {1} successfully uploaded to {2}", Data.LocalFilename, Data.Hash, remoteSpace);
            else
                Log.Octopus().ErrorFormat("Upload of file {0} with hash {1} to {2} failed: {3}", Data.LocalFilename, Data.Hash, remoteSpace, message.Message);

            Send(Data.ReplyTo, new SendFileResult(message.Succeeded, message.Message, message.DestinationPath));
            
            Complete();
        }

        public void Receive(TimeoutElapsedEvent message)
        {
            Log.Octopus().Error("Transfer of " + Data.LocalFilename + " did not complete in " + ProcessTimeout);
            Complete();
        }
    }
}
