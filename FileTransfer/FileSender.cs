using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Messages.FileTransfer;
using Octopus.Platform.Util;
using Pipefish;
using Pipefish.Errors;
using Pipefish.Messages;
using Pipefish.Transport.SecureTcp.MessageExchange;

namespace Octopus.Shared.FileTransfer
{
    [Description("Send File")]
    public class FileSender : 
        PersistentActor<FileSendData>,
        ICreatedBy<SendFileCommand>,
        IReceiveAsync<SendNextChunkRequest>,
        IReceiveAsync<EagerTransferReceipt>,
        IReceive<FileTransferCompleteEvent>
    {
        readonly IOctopusFileSystem fileSystem;
        const int ChunkSize = 1024 * 1024;
        readonly ISupervisedActivity supervised;

        const string SendFile = "Send File";

        static readonly TimeSpan ProgressReportInterval = TimeSpan.FromSeconds(10);

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
                Destination = message.RemoteSquid,
                EagerChunksAhead = 0
            };

            supervised.Activity.Verbose("Requesting upload...");

            var begin = Dispatch(message.RemoteSquid, new BeginFileTransferCommand(Path.GetFileName(Data.LocalFilename), Data.Hash, Data.ExpectedSize), isTracked: true);
            supervised.BeginOperation(SendFile, begin.Id);
        }

        string CalculateHash(string localFilename)
        {
            using (var file = fileSystem.OpenFile(localFilename, FileAccess.Read))
                return HashCalculator.Hash(file);
        }

        public Task ReceiveAsync(EagerTransferReceipt message)
        {
            Data.EagerChunksAhead++;
            return ReplyWithNextChunk(message);
        }

        public async Task ReceiveAsync(SendNextChunkRequest message)
        {
            if (Data.EagerChunksAhead > 0)
            {
                Reply(message, new ChunkAlreadySentAcknowledgement(), isTracked: true);
                Data.EagerChunksAhead--;
                return;
            }

            await ReplyWithNextChunk(message);
        }

        async Task ReplyWithNextChunk(IMessage message)
        {
            var nextChunkOffset = Data.NextChunkIndex * ChunkSize;

            using (var file = fileSystem.OpenFile(Data.LocalFilename, FileAccess.Read))
            {
                if (!Data.LastProgressReport.HasValue || Data.LastProgressReport.Value + ProgressReportInterval < DateTime.UtcNow)
                {
                    var percentage = (int)((double)nextChunkOffset / Data.ExpectedSize * 100.00);
                    supervised.Activity.UpdateProgressFormat(percentage, "Uploaded {0} of {1}", (nextChunkOffset > 0 ? nextChunkOffset - 1 : 0).ToFileSizeString(), Data.ExpectedSize.ToFileSizeString());
                    Data.LastProgressReport = DateTime.UtcNow;
                }

                file.Seek(nextChunkOffset, SeekOrigin.Begin);
                var bytes = new byte[ChunkSize];
                var read = await file.ReadAsync(bytes, 0, ChunkSize);
                if (read != ChunkSize)
                    Array.Resize(ref bytes, read);
                var chunk = new SendNextChunkReply(bytes, read != ChunkSize);

                Data.NextChunkIndex++;
                Reply(message, chunk, isTracked: true);
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
            // File receiver, for whatever reason, isnt' an activity. This ensures
            // we log its failures correctly, while we should convert the receiver to a
            // SupervisedActivity.

            var message = string.Format("Upload of file {0} with hash {1} to {2} failed", Data.LocalFilename, Data.Hash, Data.Destination);
            supervised.Fail(message, error.ToException());
            return Intervention.NotHandled;
        }
    }
}
