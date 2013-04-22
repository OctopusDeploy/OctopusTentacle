using System;
using System.IO;

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

        public string CacheDirectory
        {
            get
            {
                var path = EmbeddedDatabaseStoragePath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                }

                path = Path.Combine(path, "PackageCache");

                return path;
            }
        }

        public string PackagesDirectory
        {
            get { return registry.Get("Octopus.Storage.PackagesDirectoryPath", Path.Combine(EmbeddedDatabaseStoragePath, "Packages")); }
            set { registry.Set("Octopus.Storage.PackagesDirectoryPath", value); }
        }

        public string PackagesIndexDirectory
        {
            get
            {
                var path = EmbeddedDatabaseStoragePath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                }

                path = Path.Combine(path, "PackageIndex");

                return path;
            }
        }

        public int RavenPort
        {
            get { return registry.Get("Octopus.Raven.Port", 10930); }
            set { registry.Set("Octopus.Raven.Port", value); }
        }

        public string RavenHostName
        {
            get { return registry.Get("Octopus.Raven.HostName", "localhost"); }
            set { registry.Set("Octopus.Raven.HostName", value); }
        }

        public string PublicWebPortalAddress
        {
            get { return registry.Get("Octopus.Web.PublicWebPortalAddress", string.Empty); }
            set { registry.Set("Octopus.Web.PublicWebPortalAddress", value); }
        }

        public string LocalWebPortalAddress
        {
            get { return registry.Get("Octopus.Web.LocalWebPortalAddress", string.Empty); }
            set { registry.Set("Octopus.Web.LocalWebPortalAddress", value); }
        }

        public bool LocalWebPortalAddressAutoConfigure
        {
            get { return registry.Get("Octopus.Web.LocalWebPortalAddressAutoConfigure", true); }
            set { registry.Set("Octopus.Web.LocalWebPortalAddressAutoConfigure", value); }
        }
    }
}