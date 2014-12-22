using System;
using Octopus.Shared.Logging;
using Octopus.Shared.Messages.Conversations;
using Octopus.Shared.Packages;

namespace Octopus.Shared.Messages.PackageUpload
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
