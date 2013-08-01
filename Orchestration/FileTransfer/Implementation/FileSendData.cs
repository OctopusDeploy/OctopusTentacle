using System;
using Pipefish.Core;

namespace Octopus.Shared.Orchestration.FileTransfer.Implementation
{
    public class FileSendData
    {
        public string LocalFilename { get; set; }
        public string Hash { get; set; }
        public int NextChunkIndex { get; set; }
        public ActorId ReplyTo { get; set; }
        public long ExpectedSize { get; set; }
    }
}
