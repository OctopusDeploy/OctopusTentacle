using System;
using Pipefish;

namespace Octopus.Shared.FileTransfer
{
    public class FileTransferCompleteEvent : IMessage
    {
        public string DestinationPath { get; private set; }

        public FileTransferCompleteEvent(string destinationPath)
        {
            DestinationPath = destinationPath;
        }
    }
}
