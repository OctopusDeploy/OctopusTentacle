using System;
using Pipefish;

namespace Octopus.Shared.Platform.FileTransfer
{
    public class FileSentEvent : IMessage
    {
        public string DestinationPath { get; private set; }

        public FileSentEvent(string destinationPath)
        {
            DestinationPath = destinationPath;
        }
    }
}
