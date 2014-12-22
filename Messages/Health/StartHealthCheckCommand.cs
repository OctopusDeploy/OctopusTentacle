using System;
using System.ComponentModel.Design;
using Octopus.Platform.Deployment.Logging;

namespace Octopus.Platform.Deployment.Messages.Health
{
    public class StartHealthCheckCommand : ICorrelatedMessage
    {
        public LoggerReference Logger { get; private set; }
        public TimeSpan? Timeout { get; private set; }
        public TimeSpan? MachineTimeout { get; private set; }
        public string EnvironmentId { get; private set; }
        public string[] MachineIds { get; private set; }

        public StartHealthCheckCommand(LoggerReference logger, TimeSpan? timeout = null, string environmentId = null, TimeSpan? machineTimeout = null, string[] machineIds = null)
        {
            if (timeout == null && machineTimeout == null)
            {
                Timeout = TimeSpan.FromMinutes(5);
            }

            Logger = logger;
            Timeout = timeout;
            EnvironmentId = environmentId;
            MachineIds = machineIds;
            MachineTimeout = machineTimeout;
        }
    }
}