using System;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Orchestration.FileTransfer
{
    public class FileSendData
    {
        public string LocalFilename { get; set; }
        public string Hash { get; set; }
        public long NextChunkIndex { get; set; }
        public long ExpectedSize { get; set; }
        public LoggerReference Logger { get; set; }
    }
}
