using System;
using Pipefish;

namespace Octopus.Platform.Deployment.Messages.Deploy.Acquire
{
    public class PackageAcquiredEvent : IMessage
    {
        public AcquiredPackage AcquiredPackage { get; private set; }

        public PackageAcquiredEvent(AcquiredPackage acquiredPackage)
        {
            AcquiredPackage = acquiredPackage;
        }
    }
}