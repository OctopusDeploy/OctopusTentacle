using System;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.FileTransfer
{
    public class SendFileRequest : IMessageWithLogger
    {
        public string LocalFilename { get; private set; }
        public string RemoteSquid { get; private set; }
        public long ExpectedSize { get; private set; }
        public string Hash { get; private set; }
        public LoggerReference Logger { get; private set; }

        public SendFileRequest(string localFilename, string remoteSquid, long expectedSize, string hash, LoggerReference logger)
        {
            LocalFilename = localFilename;
            RemoteSquid = remoteSquid;
            ExpectedSize = expectedSize;
            Hash = hash;
            Logger = logger;
        }
    }
}
