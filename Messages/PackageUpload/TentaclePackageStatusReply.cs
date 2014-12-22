using System;
using Pipefish;

namespace Octopus.Platform.Deployment.Messages.PackageUpload
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
