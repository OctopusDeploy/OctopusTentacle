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
                var path = registry.GetString("Tentacle.Deployment.ApplicationDirectory");
                if (string.IsNullOrWhiteSpace(path))
                {
                    var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    path = Path.Combine(Path.GetPathRoot(programFiles), "Octopus\\PackageCache");
                }

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
    }
}