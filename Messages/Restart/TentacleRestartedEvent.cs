using System;
using Pipefish;

namespace Octopus.Platform.Deployment.Messages.Restart
{
    public class TentacleRestartedEvent : IMessage
    {
        public string RunningVersion { get; private set; }

        public TentacleRestartedEvent(string runningVersion)
        {
            RunningVersion = runningVersion;
        }
    }
}
