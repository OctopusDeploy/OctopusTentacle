using System;
using System.IO;
using System.Xml.Serialization;

namespace Octopus.Shared.Configuration
{
    [XmlRoot("Octopus")]
    public class XmlOctopusConfiguration : IOctopusConfiguration
    {
        public bool BackupsEnabledByDefault { get; set; }
        public string EmbeddedDatabaseStoragePath { get; set; }
        public bool AllowCheckingForUpgrades { get; set; }
        public bool IncludeUsageStatisticsWhenCheckingForUpgrades { get; set; }
        public string CacheDirectory { get; set; }
        public string PackagesIndexDirectory { get; set; }
        public int RavenPort { get; set; }
        public string RavenHostName { get; set; }
        public string RavenConnectionString { get; set; }
        public bool SelfHostWebPortal { get; set; }
        public int SelfHostWebPortalPort { get; set; }
        public AuthenticationMode AuthenticationMode { get; set; }

        public static XmlOctopusConfiguration LoadFrom(string file)
        {
            using (var stream = new StreamReader(file))
            {
                var config = new XmlSerializer(typeof (XmlOctopusConfiguration));
                return (XmlOctopusConfiguration) config.Deserialize(stream);
            }
        }
    }
}