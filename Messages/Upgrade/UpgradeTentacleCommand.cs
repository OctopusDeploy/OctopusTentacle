using System;
using Octopus.Shared.Logging;
using Octopus.Shared.Packages;

namespace Octopus.Shared.Messages.Upgrade
{
    public class UpgradeTentacleCommand : IReusableMessage
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

        public IReusableMessage CopyForReuse(LoggerReference newLogger)
        {
            return new UpgradeTentacleCommand(TentacleSquid, Logger, Package);
        }
    }
}
