using System;
using Pipefish;

namespace Octopus.Shared.Orchestration.PackageUpload
{
    public class TentaclePackageStatusReply : IMessage
    {
        public bool IsPackagePresent { get; private set; }

        public TentaclePackageStatusReply(bool isPackagePresent)
        {
            IsPackagePresent = isPackagePresent;
        }
    }
}
