using System;
using Pipefish;

namespace Octopus.Shared.Orchestration.FileTransfer.Implementation
{
    public class FileTransferCompleteEvent : IMessage
    {
        public bool Succeeded { get; private set; }
        public bool AlreadyPresent { get; private set; }
        public string Message { get; private set; }
        public string DestinationPath { get; set; }

        public FileTransferCompleteEvent(bool succeeded, bool alreadyPresent, string message, string destinationPath)
        {
            Succeeded = succeeded;
            AlreadyPresent = alreadyPresent;
            Message = message;
            DestinationPath = destinationPath;
        }
    }
}
