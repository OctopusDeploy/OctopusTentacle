using System;
using System.IO;
using System.Xml.Serialization;

namespace Octopus.Shared.Configuration
{
    [XmlRoot("Octopus")]
    public class XmlOctopusConfiguration
    {
        [XmlIgnore]
        public bool? BackupsEnabledByDefault { get; set; }
        public string EmbeddedDatabaseStoragePath { get; set; }
        [XmlIgnore]
        public bool? AllowCheckingForUpgrades { get; set; }
        [XmlIgnore]
        public bool? IncludeUsageStatisticsWhenCheckingForUpgrades { get; set; }
        public string CacheDirectory { get; set; }
        public string PackagesIndexDirectory { get; set; }
        [XmlIgnore]
        public int? RavenPort { get; set; }
        public string RavenHostName { get; set; }
        public string RavenConnectionString { get; set; }
        [XmlIgnore]
        public bool? SelfHostWebPortal { get; set; }
        [XmlIgnore]
        public int? SelfHostWebPortalPort { get; set; }
        [XmlIgnore]
        public AuthenticationMode? AuthenticationMode { get; set; }

        [XmlElement("BackupsEnabledByDefault")]
        public string BackupsEnabledByDefaultAsString 
        {
            get { return BackupsEnabledByDefault.HasValue ? BackupsEnabledByDefault.ToString() : null; }
            set { BackupsEnabledByDefault = !string.IsNullOrWhiteSpace(value) ? bool.Parse(value) : default(bool?); }
        }

        [XmlElement("AllowCheckingForUpgrades")]
        public string AllowChecingForUpgradesAsString
        {
            get { return AllowCheckingForUpgrades.HasValue ? AllowCheckingForUpgrades.ToString() : null; }
            set { AllowCheckingForUpgrades = !string.IsNullOrWhiteSpace(value) ? bool.Parse(value) : default(bool?); }
        }

        [XmlElement("IncludeUsageStatisticsWhenCheckingForUpgrades")]
        public string IncludeUsageStatisticsWhenCheckingForUpgradesAsString
        {
            get { return IncludeUsageStatisticsWhenCheckingForUpgrades.HasValue ? IncludeUsageStatisticsWhenCheckingForUpgrades.ToString() : null; }
            set { IncludeUsageStatisticsWhenCheckingForUpgrades = !string.IsNullOrWhiteSpace(value) ? bool.Parse(value) : default(bool?); }
        }

        [XmlElement("RavenPort")]
        public string RavenPortAsString
        {
            get { return RavenPort.HasValue ? RavenPort.ToString() : null; }
            set { RavenPort = !string.IsNullOrWhiteSpace(value) ? int.Parse(value) : default(int?); }
        }

        [XmlElement("SelfHostWebPortal")]
        public string SelfHostWebPortalAsString
        {
            get { return SelfHostWebPortal.HasValue ? SelfHostWebPortal.ToString() : null; }
            set { SelfHostWebPortal = !string.IsNullOrWhiteSpace(value) ? bool.Parse(value) : default(bool?); }
        }

        [XmlElement("AuthenticationMode")]
        public string AuthenticationModeAsString
        {
            get { return AuthenticationMode.HasValue ? AuthenticationMode.ToString() : null; }
            set { AuthenticationMode = !string.IsNullOrWhiteSpace(value) ? (AuthenticationMode) Enum.Parse(typeof (AuthenticationMode), value) : default(AuthenticationMode); }
        }
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