using System;
using Pipefish;

namespace Octopus.Shared.Platform.PackageUpload
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
