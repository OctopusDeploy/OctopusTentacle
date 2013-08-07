using System;
using Octopus.Shared.Platform.Conversations;
using Octopus.Shared.Platform.Logging;
using Pipefish.Toolkit.Supervision;

namespace Octopus.Shared.Platform.FileTransfer
{
    [BeginsConversationEndedBy(typeof(FileSentEvent), typeof(CompletionEvent))]
    public class SendFileCommand : IMessageWithLogger
    {
        public string LocalFilename { get; private set; }
        public string RemoteSquid { get; private set; }
        public long ExpectedSize { get; private set; }
        public string Hash { get; private set; }
        public LoggerReference Logger { get; private set; }

        public SendFileCommand(string localFilename, string remoteSquid, long expectedSize, string hash, LoggerReference logger)
        {
            LocalFilename = localFilename;
            RemoteSquid = remoteSquid;
            ExpectedSize = expectedSize;
            Hash = hash;
            Logger = logger;
        }
    }
}
