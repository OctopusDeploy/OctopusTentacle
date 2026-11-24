using System;
using System.Collections.Generic;

namespace Octopus.Tentacle.Configuration
{
    public class WatchdogConfiguration
    {
        public WatchdogConfiguration(bool enabled, int interval, string instances)
        {
            Enabled = enabled;
            Interval = interval;
            Instances = instances;
        }

        public bool Enabled { get; }
        public int Interval { get; }
        public string Instances { get; }

        public IEnumerable<KeyValuePair<string, object?>> DisplaySettings()
        {
            yield return new KeyValuePair<string, object?>("Octopus.Watchdog.Enabled", Enabled);
            yield return new KeyValuePair<string, object?>("Octopus.Watchdog.Interval", Interval);
            yield return new KeyValuePair<string, object?>("Octopus.Watchdog.Instances", Instances);
        }
    }
}