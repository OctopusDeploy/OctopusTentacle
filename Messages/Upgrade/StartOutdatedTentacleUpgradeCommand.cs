using System;
using Octopus.Shared.Logging;

namespace Octopus.Shared.Messages.Upgrade
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
