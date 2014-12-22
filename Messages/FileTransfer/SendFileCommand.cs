using System;
using Octopus.Shared.Logging;
using Octopus.Shared.Messages.Conversations;

namespace Octopus.Shared.Messages.FileTransfer
{
    [ExpectReply]
    public class SendFileCommand : ICorrelatedMessage
    {
        public string LocalFilename { get; private set; }
        public string RemoteSquid { get; private set; }
        public long? ExpectedSize { get; private set; }
        public string Hash { get; private set; }
        public LoggerReference Logger { get; private set; }

        public SendFileCommand(LoggerReference logger, string localFilename, string remoteSquid, long? expectedSize, string hash)
        {
            LocalFilename = localFilename;
            RemoteSquid = remoteSquid;
            ExpectedSize = expectedSize;
            Hash = hash;
            Logger = logger;
        }
    }
}
