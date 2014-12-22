using System;
using System.Collections.Generic;
using Pipefish;

namespace Octopus.Platform.Deployment.Messages.Health
{
    public class TentacleReportHealthReply : IMessage
    {
        public string MachineName { get; set; }
        public string RunningAs { get; set; }
        public string Version { get; set; }
        public Dictionary<string, long> FreeDiskSpace { get; set; } 

        public TentacleReportHealthReply(string machineName, string runningAs, string version, Dictionary<string, long> freeDiskSpace)
        {
            MachineName = machineName;
            RunningAs = runningAs;
            Version = version;
            FreeDiskSpace = freeDiskSpace;
        }
    }
}
