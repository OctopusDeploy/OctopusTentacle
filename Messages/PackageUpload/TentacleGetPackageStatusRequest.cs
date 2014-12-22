using System;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Messages.Conversations;
using Octopus.Platform.Deployment.Packages;

namespace Octopus.Platform.Deployment.Messages.PackageUpload
{
    [ExpectReply]
    public class TentacleGetPackageStatusRequest : ICorrelatedMessage
    {
        public LoggerReference Logger { get; private set; }
        public PackageMetadata Package { get; private set; }

        public TentacleGetPackageStatusRequest(LoggerReference logger, PackageMetadata package)
        {
            Logger = logger;
            Package = package;
        }
    }
}
