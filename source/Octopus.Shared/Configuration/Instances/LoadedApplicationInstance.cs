using System;
using Octopus.Configuration;

namespace Octopus.Shared.Configuration.Instances
{
    public interface ILoadedApplicationInstance
    {
        string InstanceName { get; }
        
        public IKeyValueStore Configuration { get; }
    }

    public class LoadedApplicationInstance : ILoadedApplicationInstance
    {
        public LoadedApplicationInstance(string instanceName, IKeyValueStore configuration)
        {
            InstanceName = instanceName;
            Configuration = configuration;
        }

        public string InstanceName { get; }

        public IKeyValueStore Configuration { get; }
    }

    public interface ILoadedPersistedApplicationInstance : ILoadedApplicationInstance
    {
        IModifiableKeyValueStore ModifiableConfiguration { get; }
    }
    
    public class LoadedPersistedApplicationInstance : LoadedApplicationInstance, ILoadedPersistedApplicationInstance
    {
        public LoadedPersistedApplicationInstance(string instanceName, IModifiableKeyValueStore configuration, string configurationPath) : base(instanceName, configuration)
        {
            ConfigurationPath = configurationPath;
            ModifiableConfiguration = configuration;
        }

        public IModifiableKeyValueStore ModifiableConfiguration { get; }

        public string ConfigurationPath { get; }
    }
}