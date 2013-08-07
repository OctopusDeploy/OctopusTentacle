using System;
using Octopus.Shared.Contracts;
using Octopus.Shared.Platform.Conversations;
using Pipefish;

namespace Octopus.Shared.Platform.PackageUpload
{
    [ExpectReply]
    public class TentacleGetPackageStatusRequest : IMessage
    {
        public PackageMetadata Package { get; private set; }

        public TentacleGetPackageStatusRequest(PackageMetadata package)
        {
            Package = package;
        }
    }
}
