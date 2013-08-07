using System;
using Octopus.Shared.Contracts;
using Octopus.Shared.Platform.Conversations;
using Octopus.Shared.Platform.Logging;
using Pipefish.Toolkit.Supervision;

namespace Octopus.Shared.Platform.Deploy.Acquire
{
    [BeginsConversationEndedBy(typeof(TentaclePackageDownloadedEvent), typeof(CompletionEvent))]
    public class TentacleDownloadPackageCommand : IMessageWithLogger
    {
        public LoggerReference Logger { get; private set; }
        public NuGetFeedProperties FeedProperties { get; private set; }
        public PackageMetadata Package { get; private set; }
        public bool ForcePackageDownload { get; private set; }

        public TentacleDownloadPackageCommand(
            LoggerReference logger,
            NuGetFeedProperties feedProperties,
            PackageMetadata package,
            bool forcePackageDownload)
        {
            Logger = logger;
            FeedProperties = feedProperties;
            Package = package;
            ForcePackageDownload = forcePackageDownload;
        }
    }
}