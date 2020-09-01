using System;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration.Instances
{
    public class LoadedApplicationInstance
    {
        public LoadedApplicationInstance(string instanceName, IKeyValueStore configuration)
        {
            InstanceName = instanceName;
            Configuration = configuration;
        }

        public string InstanceName { get; }

        public IKeyValueStore Configuration { get; }
    }

    public class LoadedPersistedApplicationInstance : LoadedApplicationInstance
    {
        public LoadedPersistedApplicationInstance(string instanceName, IKeyValueStore configuration, string configurationPath) : base(instanceName, configuration)
        {
            ConfigurationPath = configurationPath;
        }

        public string ConfigurationPath { get; }
    }
}