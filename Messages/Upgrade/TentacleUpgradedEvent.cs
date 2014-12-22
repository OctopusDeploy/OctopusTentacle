using System;
using Pipefish;

namespace Octopus.Platform.Deployment.Messages.Upgrade
{
    public class TentacleUpgradedEvent : IMessage
    {
        public string TentacleSquid { get; set; }
        public string Version { get; private set; }

        public TentacleUpgradedEvent(string tentacleSquid, string version)
        {
            TentacleSquid = tentacleSquid;
            Version = version;
        }
    }
}
