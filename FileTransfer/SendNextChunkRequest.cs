using System;
using Octopus.Shared.Messages.Conversations;
using Pipefish;

namespace Octopus.Shared.FileTransfer
{
    [ExpectReply]
    public class SendNextChunkRequest : IMessage
    {
        public bool SupportsEagerTransfer { get; set; }
    }
}