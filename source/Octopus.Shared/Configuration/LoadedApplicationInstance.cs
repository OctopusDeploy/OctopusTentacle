using System;
using Octopus.Configuration;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class LoadedApplicationInstance
    {
        readonly ApplicationName applicationName;
        readonly string instanceName;
        readonly string configurationPath;
        readonly IKeyValueStore configuration;

        public LoadedApplicationInstance(ApplicationName applicationName, string instanceName, string configurationPath, IKeyValueStore configuration)
        {
            this.applicationName = applicationName;
            this.instanceName = instanceName;
            this.configurationPath = configurationPath;
            this.configuration = configuration;
        }

        public string InstanceName
        {
            get { return instanceName; }
        }

        public ApplicationName ApplicationName
        {
            get { return applicationName; }
        }

        public string ApplicationDescription
        {
            get { return ApplicationName.GetDescription(); }
        }

        public IKeyValueStore Configuration
        {
            get { return configuration; }
        }

        public string ConfigurationPath
        {
            get { return configurationPath; }
        }
    }
}