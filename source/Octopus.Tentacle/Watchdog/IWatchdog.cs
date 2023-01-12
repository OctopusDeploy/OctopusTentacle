using System;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Watchdog
{
    public interface IWatchdog
    {
        void Delete();
        void Create(string instanceNames, int interval);
        WatchdogConfiguration GetConfiguration();
    }
}