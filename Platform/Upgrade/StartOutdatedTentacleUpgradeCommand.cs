using System;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Upgrade
{
    public class StartOutdatedTentacleUpgradeCommand : IMessageWithLogger
    {
        public LoggerReference Logger { get; private set; }

        public StartOutdatedTentacleUpgradeCommand(LoggerReference logger)
        {
            Logger = logger;
        }
    }
}
