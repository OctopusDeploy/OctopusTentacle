using System;
using Octopus.Shared.Contracts;
using Octopus.Shared.Platform.Conversations;
using Pipefish;

namespace Octopus.Shared.Platform.PackageUpload
{
    [BeginsConversationEndedBy(typeof(TentaclePackageStatusReply))]
    public class TentacleGetPackageStatusRequest : IMessage
    {
        public PackageMetadata Package { get; private set; }

        public TentacleGetPackageStatusRequest(PackageMetadata package)
        {
            Package = package;
        }
    }
}
