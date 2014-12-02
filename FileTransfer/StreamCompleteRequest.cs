using System;
using Pipefish;
using Pipefish.Streaming;

namespace Octopus.Shared.FileTransfer
{
    public class StreamCompleteRequest : IMessage
    {
        public StreamReceipt Receipt { get; private set; }

        public StreamCompleteRequest(StreamReceipt receipt)
        {
            Receipt = receipt;
        }
    }
}