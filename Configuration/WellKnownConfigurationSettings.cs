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
    }
}