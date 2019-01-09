using System;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;

namespace Octopus.Shared.Services
{
    public class NullWatchdog : IWatchdog
    {
        private static WatchdogConfiguration EmptyWatchdogConfiguration = new WatchdogConfiguration(false, 0, string.Empty);
        readonly ILog log;
        readonly string taskName;

        public NullWatchdog(ApplicationName applicationName, ILog log)
        {
            taskName = "Octopus Watchdog " + applicationName;
            this.log = log;
        }
        public void Delete()
        {
            // nothing to see here
        }

        public void Create(string instanceNames, int interval)
        {
            // nothing to see here
        }

        public WatchdogConfiguration GetConfiguration()
        {
            // nothing to see here
            return EmptyWatchdogConfiguration;
        }

        public bool IsAvailable => false;
    }
}