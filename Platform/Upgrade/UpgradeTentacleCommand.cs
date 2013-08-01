using System;
using Octopus.Shared.Packages;
using Pipefish;

namespace Octopus.Shared.Platform.Upgrade
{
    public class UpgradeTentacleCommand : IMessage
    {
        public string TentacleSquid { get; set; }
        public StoredPackage Package { get; set; }
    }
}
