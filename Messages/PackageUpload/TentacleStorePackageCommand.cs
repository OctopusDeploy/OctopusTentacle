using System;
using Octopus.Shared.Logging;
using Octopus.Shared.Messages.Conversations;
using Octopus.Shared.Packages;

namespace Octopus.Shared.Messages.PackageUpload
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
