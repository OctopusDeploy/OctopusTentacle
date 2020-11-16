using System;
using Octopus.Shared.Configuration;

namespace Octopus.Shared.Services
{
    public class NullWatchdog : IWatchdog
    {
        static readonly WatchdogConfiguration EmptyWatchdogConfiguration = new WatchdogConfiguration(false, 0, "*");

        public void Delete()
        {
            throw new ControlledFailureException("Watchdog is not supported on this operating system.");
        }

        public void Create(string instanceNames, int interval)
        {
            throw new ControlledFailureException("Watchdog is not supported on this operating system.");
        }

        public WatchdogConfiguration GetConfiguration()
            // nothing to see here
            => EmptyWatchdogConfiguration;
    }
}