using System;
using Pipefish;

namespace Octopus.Shared.Orchestration.FileTransfer.Implementation
{
    public class FileTransferCompleteEvent : IMessage
    {
        public bool Succeeded { get; private set; }
        public string Message { get; private set; }
        public string DestinationPath { get; set; }

        public FileTransferCompleteEvent(bool succeeded, string message, string destinationPath)
        {
            Succeeded = succeeded;
            Message = message;
            DestinationPath = destinationPath;
        }
    }
}
