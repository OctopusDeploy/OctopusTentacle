using System;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration
{
    public class BundledPackageStoreConfiguration : IBundledPackageStoreConfiguration
    {
        readonly IKeyValueStore settings;

        public BundledPackageStoreConfiguration(IKeyValueStore settings)
        {
            this.settings = settings;
        }
        
        public string CustomPackageDirectory
        {
            get { return settings.Get("Octopus.Deployment.CustomBundledPackageDirectory", string.Empty); }
            set { settings.Set("Octopus.Deployment.CustomBundledPackageDirectory", value); }
        }
    }
}