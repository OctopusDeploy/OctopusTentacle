using System;
using System.Collections.Generic;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Platform.Deployment.Acquire
{
    public class AcquiredPackage
    {
        public IList<string> Steps { get; private set; }
        public PackageMetadata Package { get; private set; }

        public AcquiredPackage(IList<string> steps, PackageMetadata package)
        {
            Steps = steps;
            Package = package;
        }
    }
}