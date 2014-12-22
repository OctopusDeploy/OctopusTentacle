using System;
using System.Collections.Generic;
using Octopus.Platform.Deployment.Packages;

namespace Octopus.Platform.Deployment.Messages.Deploy.Acquire
{
    public class AcquiredPackage
    {
        public List<string> ActionIds { get; private set; }
        public PackageMetadata Package { get; private set; }

        public AcquiredPackage(List<string> actionIds, PackageMetadata package)
        {
            ActionIds = actionIds;
            Package = package;
        }
    }
}