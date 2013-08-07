using System;
using Octopus.Shared.Contracts;
using Octopus.Shared.Platform.Deployment;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Deploy.Package
{
    public class StartPackageStepCommand : StartDeploymentStepCommand
    {
        public PackageMetadata Package { get; set; }

        public StartPackageStepCommand(LoggerReference logger, string deploymentId, string deploymentStepId, PackageMetadata package) 
            : base(logger, deploymentId, deploymentStepId)
        {
            Package = package;
        }
    }
}
