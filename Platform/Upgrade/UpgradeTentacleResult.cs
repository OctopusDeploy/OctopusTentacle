using System;

namespace Octopus.Shared.Orchestration.Upgrade
{
    public class UpgradeTentacleResult : ResultMessage
    {
        public string Version { get; private set; }

        public UpgradeTentacleResult(bool wasSuccessful, string details, string version)
            : base(wasSuccessful, details)
        {
            Version = version;
        }
    }
}
