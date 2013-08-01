using System;
using Octopus.Shared.Platform.Logging;
using Octopus.Shared.Platform.ServerTasks;

namespace Octopus.Shared.Platform.Upgrade
{
    public class StartOutdatedTentacleUpgradeCommand : IStartOrchestrationCommand
    {
        public LoggerReference Logger { get; private set; }
        public string TaskId { get; private set; }

        public StartOutdatedTentacleUpgradeCommand(LoggerReference logger, string taskId)
        {
            Logger = logger;
            TaskId = taskId;
        }
    }
}
