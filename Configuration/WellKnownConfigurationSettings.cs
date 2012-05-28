using System.Linq;

namespace Octopus.Shared.Configuration
{
    public static class WellKnownConfigurationSettings
    {
        /// <summary>
        /// This setting tells RavenDB where to store its data.
        /// </summary>
        public static string StoragePath = "Octopus.Storage.Path";

        /// <summary>
        /// A comma separated list of trusted Octopus certificate thumbprints.
        /// </summary>
        public static string TrustedOctopusThumbprints = "Tentacle.Security.TrustedOctopusThumbprints";

        public static string CheckForUpgrades = "Octopus.Upgrades.AllowChecking";
        
        public static string IncludeUsageStatistics = "Octopus.Upgrades.IncludeStatistics";

        public static string[] GetTrustedOctopusThumbprints(this IGlobalConfiguration globalConfiguration)
        {
            var value = globalConfiguration.Get(TrustedOctopusThumbprints);
            if (string.IsNullOrWhiteSpace(value))
            {
                return new string[0];
            }

            return value.Split(';', ',').Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).Distinct().ToArray();
        }

        public static void SetTrustedOctopusThumbprints(this IGlobalConfiguration globalConfiguration, string[] thumbprints)
        {
            globalConfiguration.Set(TrustedOctopusThumbprints, string.Join(",", thumbprints));
        }

        public static bool IsCheckForUpgradesEnabled(this IGlobalConfiguration globalConfiguration)
        {
            return GetBool(globalConfiguration, CheckForUpgrades, true);
        }

        public static bool IsIncludeUsageStatisticsEnabled(this IGlobalConfiguration globalConfiguration)
        {
            return GetBool(globalConfiguration, IncludeUsageStatistics, true);
        }

        public static void SetCheckForUpgradesEnabled(this IGlobalConfiguration globalConfiguration, bool value)
        {
            globalConfiguration.Set(CheckForUpgrades, value.ToString());
        }

        public static void SetIncludeUsageStatisticsEnabled(this IGlobalConfiguration globalConfiguration, bool value)
        {
            globalConfiguration.Set(IncludeUsageStatistics, value.ToString());
        }

        static bool GetBool(IGlobalConfiguration configuration, string name, bool defaultValue)
        {
            var value = configuration.Get(name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                bool result;
                if (bool.TryParse(value, out result))
                {
                    return result;
                }
            }

            return defaultValue;
        }
    }
}