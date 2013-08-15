using System;
using System.IO;
using System.Threading.Tasks;
using Octopus.Shared.Orchestration.Completion;
using Octopus.Shared.Orchestration.Logging;
using Octopus.Shared.Platform.FileTransfer;
using Octopus.Shared.Util;
using Pipefish;
using Pipefish.Toolkit.Supervision;

namespace Octopus.Shared.Orchestration.FileTransfer
{
    public class FileSender : 
        PersistentActor<FileSendData>,
        ICreatedBy<SendFileCommand>,
        IReceiveAsync<SendNextChunkRequest>,
        IReceive<FileTransferCompleteEvent>
    {
        readonly IOctopusFileSystem fileSystem;
        readonly IActivity activity;
        const int ChunkSize = 128 * 1024;
        readonly Supervised supervised;

        public FileSender(IOctopusFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
            activity = RegisterAspect<Activity>();
            supervised = RegisterAspect<Supervised>();
            RegisterAspect(new CompletesOnTimeout(() => activity.ErrorFormat("Transfer of {0} did not complete before the process timeout", Data.LocalFilename)));
        }

        public void Receive(SendFileCommand message)
        {
            Data = new FileSendData
            { 
                LocalFilename = message.LocalFilename, 
                Hash = message.Hash ?? CalculateHash(message.LocalFilename),
                NextChunkIndex = 0,
                ExpectedSize = message.ExpectedSize ?? fileSystem.GetFileSize(message.LocalFilename),
                Logger = message.Logger
            };

            activity.Verbose("Requesting upload...");

            Dispatch(message.RemoteSquid, new BeginFileTransferCommand(Path.GetFileName(Data.LocalFilename), Data.Hash, Data.ExpectedSize));
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
                activity.UpdateProgressFormat(percentage, "Uploaded {0} of {1}", nextChunkOffset.ToFileSizeString(), Data.ExpectedSize.ToFileSizeString());
                
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

            if (message.Succeeded)
            {
                activity.UpdateProgressFormat(100, "Uploaded {0}", Data.ExpectedSize.ToFileSizeString());
                activity.InfoFormat("File {0} with hash {1} successfully uploaded to {2}", Data.LocalFilename, Data.Hash, remoteSpace);
                supervised.Succeed(new FileSentEvent(message.DestinationPath));
            }
            else
            {
                activity.ErrorFormat("Upload of file {0} with hash {1} to {2} failed: {3}", Data.LocalFilename, Data.Hash, remoteSpace, message.Message);
                supervised.Fail(message.Message);
            }
        }
    }
}
