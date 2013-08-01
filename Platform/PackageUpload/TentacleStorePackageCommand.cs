using System;
using Octopus.Shared.Contracts;
using Octopus.Shared.Platform.Conversations;
using Pipefish;

namespace Octopus.Shared.Platform.PackageUpload
{
    [BeginsConversationEndedBy(typeof(TentaclePackageStoredResult))]
    public class TentacleStorePackageCommand : IMessage
    {
        public PackageMetadata Package { get; private set; }
        public string UploadedFilePath { get; private set; }

        public TentacleStorePackageCommand(PackageMetadata package, string uploadedFilePath)
        {
            Package = package;
            UploadedFilePath = uploadedFilePath;
        }
    }
}
