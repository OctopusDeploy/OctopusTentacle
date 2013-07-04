using System;
using System.IO;

namespace Octopus.Shared.Configuration
{
    public class DeploymentProcessConfiguration : IDeploymentProcessConfiguration
    {
        readonly IHomeConfiguration home;

        public DeploymentProcessConfiguration(IHomeConfiguration home)
        {
            this.home = home;
        }

        public string CacheDirectory
        {
            get
            {
                return Path.Combine(home.HomeDirectory, "PackageCache");
            }
        }
    }
}