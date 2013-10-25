using System;
using System.IO;
using System.Threading.Tasks;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Messages.FileTransfer;
using Octopus.Platform.Util;
using Pipefish;
using Pipefish.Messages;

namespace Octopus.Shared.FileTransfer
{
    public class FileSender : 
        PersistentActor<FileSendData>,
        ICreatedBy<SendFileCommand>,
        IReceiveAsync<SendNextChunkRequest>,
        IReceive<FileTransferCompleteEvent>
    {
        readonly IOctopusFileSystem fileSystem;
        const int ChunkSize = 4 * 1024;
        readonly ISupervisedActivity supervised;

        const string SendFile = "Send File";

        public FileSender(IOctopusFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
            supervised = RegisterAspect(new SupervisedActivity(config =>
            {
                config.Operation(SendFile).OnItemFailure(SendFileFailure);
                config.OnProcessTimeout(() => supervised.Activity.ErrorFormat("Transfer of {0} did not complete before the process timeout", Data.LocalFilename));
            }));
        }

        public void Receive(SendFileCommand message)
        {
            Data = new FileSendData
            { 
                LocalFilename = message.LocalFilename, 
                Hash = message.Hash ?? CalculateHash(message.LocalFilename),
                NextChunkIndex = 0,
                ExpectedSize = message.ExpectedSize ?? fileSystem.GetFileSize(message.LocalFilename),
                Destination = message.RemoteSquid
            };

            supervised.Activity.Verbose("Requesting upload...");

            var begin = Dispatch(message.RemoteSquid, new BeginFileTransferCommand(Path.GetFileName(Data.LocalFilename), Data.Hash, Data.ExpectedSize));
            supervised.BeginOperation(SendFile, begin.Id);
        }

        string CalculateHash(string localFilename)
        {
            using (var file = fileSystem.OpenFile(localFilename, FileAccess.Read))
                return HashCalculator.Hash(file);
        }

        public async Task ReceiveAsync(SendNextChunkRequest message)
        {
            var nextChunkOffset = Data.NextChunkIndex * ChunkSize;

            using (var file = fileSystem.OpenFile(Data.LocalFilename, FileAccess.Read))
            {
                var percentage = (int)((double)nextChunkOffset / Data.ExpectedSize * 100.00);
                supervised.Activity.UpdateProgressFormat(percentage, "Uploaded {0} of {1}", nextChunkOffset.ToFileSizeString(), Data.ExpectedSize.ToFileSizeString());
                
                file.Seek(nextChunkOffset, SeekOrigin.Begin);
                var bytes = new byte[ChunkSize];
                var read = await file.ReadAsync(bytes, 0, ChunkSize);
                if (read != ChunkSize)
                    Array.Resize(ref bytes, read);
                var chunk = new SendNextChunkReply(bytes, read != ChunkSize);

                Data.NextChunkIndex++;
                Reply(message, chunk);
            }
        }

        public void Receive(FileTransferCompleteEvent message)
        {
            var remoteSpace = message.GetMessage().From.Space;

            supervised.Activity.UpdateProgressFormat(100, "Uploaded {0}", Data.ExpectedSize.ToFileSizeString());
            supervised.Activity.VerboseFormat("File {0} with hash {1} successfully uploaded to {2}", Data.LocalFilename, Data.Hash, remoteSpace);
            supervised.Succeed(new FileSentEvent(message.DestinationPath));
        }

        Intervention SendFileFailure(Guid id, Error error)
        {
            supervised.Activity.ErrorFormat("Upload of file {0} with hash {1} to {2} failed", Data.LocalFilename, Data.Hash, Data.Destination);
            return Intervention.NotHandled;
        }

    }
}
