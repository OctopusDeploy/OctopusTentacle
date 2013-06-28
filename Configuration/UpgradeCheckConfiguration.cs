using System;

namespace Octopus.Shared.Configuration
{
    public class UpgradeCheckConfiguration : IUpgradeCheckConfiguration
    {
        readonly IKeyValueStore settings;

        public UpgradeCheckConfiguration(IKeyValueStore settings)
        {
            this.settings = settings;
        }

        public bool AllowCheckingForUpgrades
        {
            get { return settings.Get("Octopus.Upgrades.AllowChecking", true); }
            set { settings.Set("Octopus.Upgrades.AllowChecking", value); }
        }

        public bool IncludeUsageStatisticsWhenCheckingForUpgrades
        {
            get { return settings.Get("Octopus.Upgrades.IncludeStatistics", true); }
            set { settings.Set("Octopus.Upgrades.IncludeStatistics", value); }
        }

        public bool ReportErrorsOnline
        {
            get { return settings.Get("Octopus.Errors.ReportErrorsOnline", true); }
            set { settings.Set("Octopus.Errors.ReportErrorsOnline", value); }
        }
    }
}