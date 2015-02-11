using System;
using System.ComponentModel;
using System.IO;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Platform.Deployment.Configuration;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Messages.FileTransfer;
using Octopus.Platform.Util;
using Pipefish;
using Pipefish.Core;
using Pipefish.Errors;
using Pipefish.Messages;
using Pipefish.Streaming;
using Pipefish.Transport;
using Pipefish.Transport.SecureTcp.MessageExchange;

namespace Octopus.Shared.FileTransfer
{
    [Description("Send File")]
    public class FileSender : 
        PersistentActor<FileSendData>,
        ICreatedBy<SendFileCommand>,
        IReceive<SendStreamRequest>,
        IReceiveAsync<SendNextChunkRequest>,
        IReceiveAsync<EagerTransferReceipt>,
        IReceive<FileTransferCompleteEvent>,
        IHandleFailed<ChunkAlreadySentAcknowledgement>,
        IHandleFailed<SendNextChunkReply>
    {
        readonly IOctopusFileSystem fileSystem;
        readonly IDistributor distributor;
        readonly ISupervisedActivity supervised;
        readonly long chunkSize;

        const string SendFile = "Send File";
        const long EagerPrefetchLimit = 5;

        static readonly TimeSpan ProgressReportInterval = TimeSpan.FromSeconds(10);

        public FileSender(IOctopusFileSystem fileSystem, ICommunicationsConfiguration comms, IDistributor distributor)
        {
            this.fileSystem = fileSystem;
            this.distributor = distributor;
            chunkSize = comms.FileTransferChunkSizeBytes;

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

            var streamClient = distributor.GetStreamClient(Data.Destination);
            var streamingSupported = streamClient != null && streamClient.IsStreamingSupported();

            var begin = Dispatch(message.RemoteSquid, new BeginFileTransferCommand(Path.GetFileName(Data.LocalFilename), Data.Hash, Data.ExpectedSize) { SupportsStreaming = streamingSupported }, isTracked: true);
            supervised.BeginOperation(SendFile, begin.Id);
        }

        string CalculateHash(string localFilename)
        {
            using (var file = fileSystem.OpenFile(localFilename, FileAccess.Read))
                return HashCalculator.Hash(file);
        }

        bool IsFinished { get { return Data.NextChunkIndex * chunkSize >= Data.ExpectedSize; } }

        public void Receive(SendStreamRequest message)
        {
            if (Data.ReceiverId == null)
                Data.ReceiverId = message.GetMessage().From;

            StreamReceipt receipt = null;
            for (var i = 1; receipt == null && i <= 5; i++)
            {
                try
                {
                    supervised.Activity.UpdateProgressFormat(1, "Begin streaming {0}", Data.ExpectedSize.ToFileSizeString());
                    receipt = SendStream();
                }
                catch (Exception ex)
                {
                    if (i < 5)
                    {
                        supervised.Activity.Info("Failed to send stream: " + ex.Message);
                        Thread.Sleep(2000);
                        supervised.Activity.Info("Retry attempt #" + i);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            supervised.Activity.Verbose("Stream transfer complete, sending stream receipt");
            var chunk = new StreamCompleteRequest(receipt);

            var reply = new Message(Id, Data.ReceiverId.Value, chunk);
            reply.Headers[Pipefish.Core.ProtocolExtensions.InReplyToHeader] = message.GetMessage().Id.ToString();
            reply.SetIsTracked(true);
            reply.SetIsEphemeral(true);
            Space.Send(reply);
        }

        StreamReceipt SendStream()
        {
            var streamClient = distributor.GetStreamClient(Data.Destination);
            var streamReceipt = streamClient.Send(stream =>
            {
                using (var file = fileSystem.OpenFile(Data.LocalFilename, FileAccess.Read))
                {
                    var buffer = new byte[1024*1024];

                    var lastPercentReported = 0;
                    var readSoFar = 0L;
                    while (true)
                    {
                        var read = file.Read(buffer, 0, buffer.Length);
                        if (read == 0)
                        {
                            break;
                        }

                        readSoFar += read;

                        stream.Write(buffer, 0, read);

                        var percentage = (int) ((double) readSoFar/Data.ExpectedSize*100.00);
                        if (percentage > lastPercentReported)
                        {
                            supervised.Activity.UpdateProgressFormat(percentage, "Uploaded {0} of {1}", readSoFar.ToFileSizeString(), Data.ExpectedSize.ToFileSizeString());
                            Data.LastProgressReport = DateTime.UtcNow;
                            lastPercentReported = percentage;
                        }
                    }
                }
                stream.Flush();
            }, Data.ExpectedSize);

            return streamReceipt;
        }

        public async Task ReceiveAsync(SendNextChunkRequest message)
        {
            if (Data.ReceiverId == null)
                Data.ReceiverId = message.GetMessage().From;

            if (Data.ExpectedSize != 0 && (Data.EagerChunksAhead > 0 || IsFinished))
            {
                Reply(message, new ChunkAlreadySentAcknowledgement(), isTracked: true);
                Data.EagerChunksAhead--;
                return;
            }

            await ReplyWithNextChunk(message, !message.SupportsEagerTransfer);
        }

        public async Task ReceiveAsync(EagerTransferReceipt message)
        {
            if (!IsFinished && Data.EagerChunksAhead <= EagerPrefetchLimit)
            {
                Data.EagerChunksAhead++;
                Data.MaxEagerChunksAhead = Math.Max(Data.EagerChunksAhead, Data.MaxEagerChunksAhead);
                await ReplyWithNextChunk(message);
            }
        }

        async Task ReplyWithNextChunk(IMessage message, bool suppressEagerTransfer = false)
        {
            if (Data.ReceiverId == null)
                throw new InvalidOperationException("Receiver ID has not been set.");

            var nextChunkOffset = Data.NextChunkIndex * chunkSize;

            using (var file = fileSystem.OpenFile(Data.LocalFilename, FileAccess.Read))
            {
                if (!Data.LastProgressReport.HasValue || Data.LastProgressReport.Value + ProgressReportInterval < DateTime.UtcNow)
                {
                    var percentage = (int)((double)nextChunkOffset / Data.ExpectedSize * 100.00);
                    supervised.Activity.UpdateProgressFormat(percentage, "Uploaded {0} of {1}", (nextChunkOffset > 0 ? nextChunkOffset - 1 : 0).ToFileSizeString(), Data.ExpectedSize.ToFileSizeString());
                    Data.LastProgressReport = DateTime.UtcNow;
                }

                file.Seek(nextChunkOffset, SeekOrigin.Begin);
                var bytes = new byte[chunkSize];
                var read = await file.ReadAsync(bytes, 0, (int)chunkSize);
                if (read != chunkSize)
                    Array.Resize(ref bytes, read);

                var isLastChunk = nextChunkOffset + read == Data.ExpectedSize;
                if (isLastChunk)
                    supervised.Activity.Verbose("Sending the last file chunk");

                var chunk = new SendNextChunkReply(bytes, isLastChunk);

                var reply = new Message(Id, Data.ReceiverId.Value, chunk);
                reply.Headers[Pipefish.Core.ProtocolExtensions.InReplyToHeader] = message.GetMessage().Id.ToString();
                reply.SetSupportsEagerTransferReceipt(!suppressEagerTransfer);
                reply.SetIsTracked(true);
                reply.SetIsEphemeral(true);
                reply.SetExpiresAt(DateTime.UtcNow.AddMinutes(10  + (5 * Data.EagerChunksAhead)));
                Space.Send(reply);

                Data.NextChunkIndex++;
            }
        }

        public void Receive(FileTransferCompleteEvent message)
        {
            var remoteSpace = message.GetMessage().From.Space;

            supervised.Activity.UpdateProgressFormat(100, "Uploaded {0}", Data.ExpectedSize.ToFileSizeString());
            supervised.Activity.VerboseFormat("File {0} with hash {1} successfully uploaded to {2}", Data.LocalFilename, Data.Hash, remoteSpace);
            if (Data.MaxEagerChunksAhead > 0)
                supervised.Activity.VerboseFormat("Eager transfer succeeded in pushing {0} chunks ({1} bytes) ahead of the receiver's acknowledgement", Data.MaxEagerChunksAhead, Data.MaxEagerChunksAhead * chunkSize);

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

        public void HandleFailed(ChunkAlreadySentAcknowledgement failedMessage, Error error)
        {
            supervised.Activity.Warn(error.ToException(), "An acknowlegement during file transfer could not be sent; this can indicate network connection quality issues.");
            // Ignore; this message's only purpose was to satisfy conversation tracking
        }

        public void HandleFailed(SendNextChunkReply failedMessage, Error error)
        {
            supervised.Fail("A request for the next file chunk could not be delivered.", error.ToException());
        }
    }
}
