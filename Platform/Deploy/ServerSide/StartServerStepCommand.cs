using System;
using Octopus.Shared.Contracts;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Deploy.ServerSide
{
    public class StartServerStepCommand: StartDeploymentStepCommand
    {
        public PackageMetadata Package { get; private set; }

        public StartServerStepCommand(LoggerReference logger, string deploymentId, string deploymentStepId, PackageMetadata package)
            : base(logger, deploymentId, deploymentStepId)
        {
            Package = package;
        }
    }
}
