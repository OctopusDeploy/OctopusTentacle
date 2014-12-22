using System;
using Octopus.Shared.Logging;
using Octopus.Shared.Messages.Conversations;
using Octopus.Shared.Packages;

namespace Octopus.Shared.Messages.Deploy.Acquire
{
    [ExpectReply]
    public class TentacleDownloadPackageCommand : IReusableMessage
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

        public IReusableMessage CopyForReuse(LoggerReference newLogger)
        {
            return new TentacleDownloadPackageCommand(newLogger, FeedProperties, Package, ForcePackageDownload);
        }
    }
}