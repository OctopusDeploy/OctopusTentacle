using System;
using Pipefish;

namespace Octopus.Shared.Platform.Health
{
    public class TentacleReportHealthReply : IMessage
    {
        public string MachineName { get; set; }
        public string RunningAs { get; set; }
        public string Version { get; set; }

        public TentacleReportHealthReply(string machineName, string runningAs, string version)
        {
            MachineName = machineName;
            RunningAs = runningAs;
            Version = version;
        }
    }
}
