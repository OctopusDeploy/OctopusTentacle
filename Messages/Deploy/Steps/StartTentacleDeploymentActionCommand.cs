using System;
using Octopus.Shared.Logging;

namespace Octopus.Shared.Messages.Deploy.Steps
{
    public abstract class StartTentacleDeploymentActionCommand : StartDeploymentActionCommand
    {
        public string MachineId { get; private set; }

        protected StartTentacleDeploymentActionCommand(LoggerReference logger, string deploymentId, string deploymentActionId, string machineId)
            : base(logger, deploymentId, deploymentActionId)
        {
            MachineId = machineId;
        }
    }
}