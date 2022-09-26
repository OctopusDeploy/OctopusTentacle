using System;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Watchdog
{
    public class NullWatchdog : IWatchdog
    {
        private static readonly WatchdogConfiguration EmptyWatchdogConfiguration = new(false, 0, "*");

        public void Delete()
        {
            throw new ControlledFailureException("Watchdog is not supported on this operating system.");
        }

        public void Create(string instanceNames, int interval)
        {
            throw new ControlledFailureException("Watchdog is not supported on this operating system.");
        }

        public WatchdogConfiguration GetConfiguration() // nothing to see here
        {
            return EmptyWatchdogConfiguration;
        }
    }
}