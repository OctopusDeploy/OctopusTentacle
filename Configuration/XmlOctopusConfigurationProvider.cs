using System;

namespace Octopus.Shared.Configuration
{
    public class XmlOctopusConfigurationProvider:IOctopusConfiguration
    {
        readonly XmlOctopusConfiguration configuration;

        public XmlOctopusConfigurationProvider(XmlOctopusConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public bool BackupsEnabledByDefault
        {
            get { return configuration.BackupsEnabledByDefault.GetValueOrDefault(true); }
            set { configuration.BackupsEnabledByDefault = value; }
        }

        //TODO: Check for relative path and supply default if not set.
        public string EmbeddedDatabaseStoragePath
        {
            get { return configuration.EmbeddedDatabaseStoragePath; }
            set { configuration.EmbeddedDatabaseStoragePath = value; }
        }

        public bool AllowCheckingForUpgrades
        {
            get { return configuration.AllowCheckingForUpgrades.GetValueOrDefault(true); }
            set { configuration.AllowCheckingForUpgrades = value; }
        }

        public bool IncludeUsageStatisticsWhenCheckingForUpgrades
        {
            get { return configuration.IncludeUsageStatisticsWhenCheckingForUpgrades.GetValueOrDefault(true); }
            set { configuration.IncludeUsageStatisticsWhenCheckingForUpgrades = value; }
        }

        //TODO: Derive from Embedded Storage Path if not supplied.
        public string CacheDirectory
        {
            get { return configuration.CacheDirectory; }
        }

        //TODO: Derive from Embedded Storage Path if not supplied.
        public string PackagesIndexDirectory
        {
            get { return configuration.PackagesIndexDirectory; }
        }

        public int RavenPort
        {
            get { return configuration.RavenPort.GetValueOrDefault(10930); }
        }

        //TODO: Supply default if not set
        public string RavenHostName
        {
            get { return configuration.RavenHostName; }
        }

        //TODO: Supply default if not set or derive from other properties.
        public string RavenConnectionString
        {
            get { return configuration.RavenConnectionString; }
        }

        public bool SelfHostWebPortal
        {
            get { return configuration.SelfHostWebPortal.GetValueOrDefault(true); }
            set { configuration.SelfHostWebPortal = value; }
        }

        public int SelfHostWebPortalPort
        {
            get { return configuration.SelfHostWebPortalPort.GetValueOrDefault(80); }
            set { configuration.SelfHostWebPortalPort = value; }
        }

        public AuthenticationMode AuthenticationMode
        {
            get { return configuration.AuthenticationMode.GetValueOrDefault(AuthenticationMode.UsernamePassword); }
            set { configuration.AuthenticationMode = value; }
        }
    }
}
