using System;
using Pipefish;

namespace Octopus.Shared.Orchestration.Restart
{
    public class TentacleRestartReply : IMessage
    {
        public bool RestartedSuccessfully { get; private set; }
        public string RunningVersion { get; private set; }

        public TentacleRestartReply(bool restartedSuccessfully, string runningVersion)
        {
            RestartedSuccessfully = restartedSuccessfully;
            RunningVersion = runningVersion;
        }
    }
}
