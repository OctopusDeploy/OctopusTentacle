using System;

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

        public void WriteTo(DictionaryKeyValueStore outputStore)
        {
            outputStore.Set("Octopus.Watchdog.Enabled", Enabled);
            outputStore.Set("Octopus.Watchdog.Interval", Interval);
            outputStore.Set<string>("Octopus.Watchdog.Instances", Instances);
        }
    }
}