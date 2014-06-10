using System;
using Pipefish;

namespace Octopus.Shared.FileTransfer
{
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
