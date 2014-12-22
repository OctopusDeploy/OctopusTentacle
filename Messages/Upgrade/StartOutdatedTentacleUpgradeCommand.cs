using System;
using Octopus.Platform.Deployment.Logging;

namespace Octopus.Platform.Deployment.Messages.Upgrade
{
    public class StartOutdatedTentacleUpgradeCommand : ICorrelatedMessage
    {
        public LoggerReference Logger { get; private set; }
        public string EnvironmentId { get; private set; }
        public string[] MachineIds { get; private set; }

        public StartOutdatedTentacleUpgradeCommand(LoggerReference logger, string environmentId, string[] machineIds)
        {
            Logger = logger;
            EnvironmentId = environmentId;
            MachineIds = machineIds;
        }
    }
}
