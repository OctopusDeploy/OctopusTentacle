using System;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Messages.Conversations;
using Octopus.Platform.Deployment.Packages;

namespace Octopus.Platform.Deployment.Messages.PackageUpload
{
    [ExpectReply]
    public class TentacleStorePackageCommand : ICorrelatedMessage
    {
        public LoggerReference Logger { get; private set; }
        public PackageMetadata Package { get; private set; }
        public string UploadedFilePath { get; private set; }

        public TentacleStorePackageCommand(LoggerReference logger, PackageMetadata package, string uploadedFilePath)
        {
            Logger = logger;
            Package = package;
            UploadedFilePath = uploadedFilePath;
        }
    }
}
