using System;
using Octopus.Platform.Deployment.Logging;
using Octopus.Platform.Deployment.Packages;

namespace Octopus.Platform.Deployment.Messages.Upgrade
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
