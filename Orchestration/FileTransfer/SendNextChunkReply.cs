using System;
using Octopus.Shared.Platform.Conversations;
using Pipefish;

namespace Octopus.Shared.Orchestration.FileTransfer
{
    [ExpectReply]
    public class SendNextChunkReply : IMessage
    {
        public byte[] Data { get; private set; }
        public bool IsLastChunk { get; private set; }

        public SendNextChunkReply(byte[] data, bool isLastChunk)
        {
            Data = data;
            IsLastChunk = isLastChunk;
        }
    }
}
