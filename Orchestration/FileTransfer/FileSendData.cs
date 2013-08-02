using System;
using Octopus.Shared.Platform.Logging;
using Pipefish.Core;

namespace Octopus.Shared.Orchestration.FileTransfer
{
    public class FileSendData
    {
        public string LocalFilename { get; set; }
        public string Hash { get; set; }
        public long NextChunkIndex { get; set; }
        public ActorId ReplyTo { get; set; }
        public long ExpectedSize { get; set; }
        public LoggerReference Logger { get; set; }
    }
}
