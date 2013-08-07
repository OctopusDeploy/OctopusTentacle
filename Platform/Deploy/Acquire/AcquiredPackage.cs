using System;
using System.Collections.Generic;
using Octopus.Shared.Contracts;

namespace Octopus.Shared.Platform.Deploy.Acquire
{
    public class AcquiredPackage
    {
        public List<string> Steps { get; private set; }
        public PackageMetadata Package { get; private set; }

        public AcquiredPackage(List<string> steps, PackageMetadata package)
        {
            Steps = steps;
            Package = package;
        }
    }
}