using System;
using Octopus.Shared.Communications.Logging;

namespace Octopus.Core.Orchestration.Messages.Upgrade
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
