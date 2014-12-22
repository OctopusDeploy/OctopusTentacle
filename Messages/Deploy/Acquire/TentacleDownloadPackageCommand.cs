using System;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Messages.Conversations;
using Octopus.Platform.Deployment.Packages;

namespace Octopus.Platform.Deployment.Messages.Deploy.Acquire
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