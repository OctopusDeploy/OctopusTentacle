using System;

namespace Octopus.Shared.Platform.Upgrade
{
    public class UpgradeTentacleResult : ResultMessage
    {
        public string TentacleSquid { get; set; }
        public string Version { get; private set; }

        public UpgradeTentacleResult(bool wasSuccessful, string details, string tentacleSquid, string version)
            : base(wasSuccessful, details)
        {
            TentacleSquid = tentacleSquid;
            Version = version;
        }
    }
}
