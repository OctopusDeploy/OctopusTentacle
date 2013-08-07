using System;
using Pipefish;

namespace Octopus.Shared.Platform.Deploy.Acquire
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