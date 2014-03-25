using System;
using Octopus.Platform.Deployment.Messages.Conversations;
using Pipefish;

namespace Octopus.Shared.FileTransfer
{
    [ExpectReply]
    public class SendNextChunkRequest : IMessage
    {
    }
}