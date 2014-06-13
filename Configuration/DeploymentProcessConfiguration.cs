using System;
using System.IO;
using Octopus.Platform.Deployment.Configuration;

namespace Octopus.Shared.Configuration
{
    public class DeploymentProcessConfiguration : IDeploymentProcessConfiguration
    {
        readonly IKeyValueStore settings;
        readonly IHomeConfiguration home;

        public DeploymentProcessConfiguration(IKeyValueStore settings, IHomeConfiguration home)
        {
            this.settings = settings;
            this.home = home;
        }

        public string CacheDirectory
        {
            get
            {
                return Path.Combine(home.HomeDirectory, "PackageCache");
            }
        }

        public int DaysToCachePackages
        {
            get { return settings.Get("Octopus.PackageCache.DaysToCachePackages", 20); }
            set { settings.Set("Octopus.PackageCache.DaysToCachePackages", value); }
        }

        public void Save()
        {
            settings.Save();
        }
    }
}