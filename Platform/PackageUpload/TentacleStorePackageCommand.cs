using System;
using Octopus.Shared.Contracts;
using Octopus.Shared.Platform.Conversations;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.PackageUpload
{
    [BeginsConversationEndedBy(typeof(TentaclePackageStoredEvent))]
    public class TentacleStorePackageCommand : IMessageWithLogger
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
