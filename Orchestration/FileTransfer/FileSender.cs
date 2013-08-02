using System;
using System.IO;
using System.Threading.Tasks;
using Octopus.Shared.Orchestration.FileTransfer.Implementation;
using Octopus.Shared.Orchestration.Logging;
using Octopus.Shared.Platform.FileTransfer;
using Octopus.Shared.Util;
using Pipefish;
using Pipefish.Messages;

namespace Octopus.Shared.Orchestration.FileTransfer
{
    public class FileSender : 
        PersistentActor<FileSendData>,
        ICreatedBy<SendFileRequest>,
        IReceiveAsync<SendNextChunkRequest>,
        IReceive<FileTransferCompleteEvent>,
        IReceive<TimeoutElapsedEvent>
    {
        readonly IOctopusFileSystem fileSystem;
        readonly IActorLog log;
        const int ChunkSize = 128 * 1024;
        readonly TimeSpan ProcessTimeout = TimeSpan.FromDays(90);

        public FileSender(IOctopusFileSystem fileSystem, IActorLog log)
        {
            this.fileSystem = fileSystem;
            this.log = RegisterAspect(log);
        }

        public void Receive(SendFileRequest message)
        {
            Data = new FileSendData
            { 
                LocalFilename = message.LocalFilename, 
                Hash = message.Hash, 
                NextChunkIndex = 0,
                ReplyTo = message.GetMessage().From,
                ExpectedSize = message.ExpectedSize,
                Logger = message.Logger
            };

            log.Verbose("Requesting upload...");

            SetTimeout(ProcessTimeout);

            Dispatch(message.RemoteSquid, new BeginFileTransferCommand(Path.GetFileName(Data.LocalFilename), Data.Hash, Data.ExpectedSize));
        }

        public async Task ReceiveAsync(SendNextChunkRequest message)
        {
            var nextChunkOffset = Data.NextChunkIndex * ChunkSize;

            using (var file = fileSystem.OpenFile(Data.LocalFilename, FileAccess.Read))
            {
                var expected = Data.ExpectedSize != 0 ? Data.ExpectedSize : file.Length;
                log.VerboseFormat("Uploaded {0} of {1} ({2:n0}%)", nextChunkOffset.ToFileSizeString(), expected.ToFileSizeString(), ((double)nextChunkOffset / Data.ExpectedSize * 100.00));
                
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
                log.InfoFormat("File {0} with hash {1} successfully uploaded to {2}", Data.LocalFilename, Data.Hash, remoteSpace);
            else
                log.ErrorFormat("Upload of file {0} with hash {1} to {2} failed: {3}", Data.LocalFilename, Data.Hash, remoteSpace, message.Message);

            Send(Data.ReplyTo, new SendFileResult(message.Succeeded, message.Message, message.DestinationPath));
            
            Complete();
        }

        public void Receive(TimeoutElapsedEvent message)
        {
            log.Error("Transfer of " + Data.LocalFilename + " did not complete in " + ProcessTimeout);
            Complete();
        }
    }
}
