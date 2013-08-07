using System;
using Pipefish;

namespace Octopus.Shared.Platform.Restart
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
