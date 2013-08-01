using System;
using Pipefish;

namespace Octopus.Shared.Orchestration.FileTransfer
{
    public class SendFileReply : IMessage
    {
        public bool SentSuccessfully { get; private set; }
        public string Error { get; private set; }

        public SendFileReply(bool sentSuccessfully, string error)
        {
            SentSuccessfully = sentSuccessfully;
            Error = error;
        }
    }
}
