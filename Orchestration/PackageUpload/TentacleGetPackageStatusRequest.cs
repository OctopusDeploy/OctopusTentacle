using System;
using Octopus.Shared.Communications.Conversations;
using Octopus.Shared.Contracts;
using Pipefish;

namespace Octopus.Shared.Orchestration.PackageUpload
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
