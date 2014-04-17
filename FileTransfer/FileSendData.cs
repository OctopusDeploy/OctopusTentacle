using System;
using Pipefish.Core;

namespace Octopus.Shared.FileTransfer
{
    public class FileSendData
    {
        public string LocalFilename { get; set; }
        public string Hash { get; set; }
        public long NextChunkIndex { get; set; }
        public long ExpectedSize { get; set; }
        public string Destination { get; set; }
        public DateTime? LastProgressReport { get; set; }
        public long EagerChunksAhead { get; set; }
        public long MaxEagerChunksAhead { get; set; }
        public ActorId? ReceiverId { get; set; }
    }
}
