using System;

namespace Octopus.Shared.Configuration
{
    public class OctopusConfiguration : IOctopusConfiguration
    {
        readonly IWindowsRegistry registry;

        public OctopusConfiguration(IWindowsRegistry registry)
        {
            this.registry = registry;
        }

        public string EmbeddedDatabaseStoragePath
        {
            get { return registry.GetString("Octopus.Storage.Path"); }
            set { registry.Set("Octopus.Storage.Path", value); }
        }

        public bool AllowCheckingForUpgrades
        {
            get { return registry.Get("Octopus.Upgrades.AllowChecking", true); }
            set { registry.Set("Octopus.Upgrades.AllowChecking", value); }
        }

        public bool IncludeUsageStatisticsWhenCheckingForUpgrades
        {
            get { return registry.Get("Octopus.Upgrades.IncludeStatistics", true); }
            set { registry.Set("Octopus.Upgrades.IncludeStatistics", value); }
        }
    }
}