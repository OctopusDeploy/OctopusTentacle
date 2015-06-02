using System;
using System.IO;

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
            get { return Path.Combine(home.ApplicationSpecificHomeDirectory, "PackageCache"); }
        }

        public int DaysToCachePackages
        {
            get { return settings.Get("Octopus.PackageCache.DaysToCachePackages", 20); }
            set { settings.Set("Octopus.PackageCache.DaysToCachePackages", value); }
        }

        public int MaxConcurrentTasks
        {
            get { return settings.Get("Octopus.Tasks.MaxConcurrentTasks", 0); }
            set { settings.Set("Octopus.Tasks.MaxConcurrentTasks", value); }
        }

        public void Save()
        {
            settings.Save();
        }
    }
}