using System;
using System.Collections.Generic;
using Octopus.Shared.Platform.Deploy.Acquire;
using Pipefish;

namespace Octopus.Shared.Platform.Deployment.Acquire
{
    public class PackagesAcquiredEvent : IMessage
    {
        public IList<AcquiredPackage> AcquiredPackages { get; private set; }

        public PackagesAcquiredEvent(IList<AcquiredPackage> acquiredPackages)
        {
            AcquiredPackages = acquiredPackages;
        }
    }
}