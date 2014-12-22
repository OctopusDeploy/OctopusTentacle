using System;
using Pipefish;

namespace Octopus.Platform.Deployment.Messages.FileTransfer
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
