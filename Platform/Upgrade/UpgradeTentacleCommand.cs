using System;
using Octopus.Shared.Packages;
using Pipefish;

namespace Octopus.Shared.Orchestration.Upgrade
{
    public class UpgradeTentacleCommand : IMessage
    {
        public string TentacleSquid { get; set; }
        public StoredPackage Package { get; set; }
    }
}
