using System;
using Octopus.Shared.Configuration;

namespace Octopus.Shared.Services
{
    public interface IWatchdog
    {
        void Delete();
        void Create(string instanceNames, int interval);
        WatchdogConfiguration GetConfiguration();
    }
}