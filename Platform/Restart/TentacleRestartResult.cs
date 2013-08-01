using System;

namespace Octopus.Shared.Platform.Restart
{
    public class TentacleRestartResult : ResultMessage
    {
        public string RunningVersion { get; private set; }

        public TentacleRestartResult(bool wasSuccessful, string description, string runningVersion)
            :base(wasSuccessful, description)
        {
            RunningVersion = runningVersion;
        }
    }
}
