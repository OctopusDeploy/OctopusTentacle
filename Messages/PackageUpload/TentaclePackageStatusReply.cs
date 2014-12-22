using System;
using Pipefish;

namespace Octopus.Shared.Messages.PackageUpload
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
