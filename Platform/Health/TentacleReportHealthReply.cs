using System;
using Pipefish;

namespace Octopus.Shared.Orchestration.Health
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
