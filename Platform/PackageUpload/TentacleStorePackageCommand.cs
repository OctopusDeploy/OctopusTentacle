using System;
using Octopus.Shared.Communications.Conversations;
using Octopus.Shared.Contracts;
using Pipefish;

namespace Octopus.Shared.Orchestration.PackageUpload
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
