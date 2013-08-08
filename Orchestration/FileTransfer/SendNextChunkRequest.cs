using System;
using Octopus.Shared.Platform.Conversations;
using Pipefish;

namespace Octopus.Shared.Orchestration.FileTransfer
{
    [ExpectReply]
    public class SendNextChunkRequest : IMessage
    {
    }
}