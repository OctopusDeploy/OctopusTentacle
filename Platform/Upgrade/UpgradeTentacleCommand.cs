using System;
using Octopus.Shared.Packages;
using Octopus.Shared.Platform.Logging;

namespace Octopus.Shared.Platform.Upgrade
{
    public class UpgradeTentacleCommand : IMessageWithLogger
    {
        public string TentacleSquid { get; private set; }
        public StoredPackage Package { get; private set; }
        public LoggerReference Logger { get; private set; }

        public UpgradeTentacleCommand(string tentacleSquid, LoggerReference logger, StoredPackage package)
        {
            TentacleSquid = tentacleSquid;
            Logger = logger;
            Package = package;
        }
    }
}
