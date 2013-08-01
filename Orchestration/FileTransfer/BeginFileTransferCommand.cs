using System;
using Octopus.Shared.Platform.Conversations;
using Pipefish;

namespace Octopus.Shared.Orchestration.FileTransfer.Implementation
{
    [BeginsConversationEndedBy(typeof(FileTransferCompleteEvent))]
    public class BeginFileTransferCommand : IMessage
    {
        public string Filename { get; private set; }
        public string Hash { get; private set; }
        public long ExpectedSize { get; private set; }

        public BeginFileTransferCommand(string filename, string hash, long expectedSize)
        {
            Filename = filename;
            Hash = hash;
            ExpectedSize = expectedSize;
        }
    }
}
