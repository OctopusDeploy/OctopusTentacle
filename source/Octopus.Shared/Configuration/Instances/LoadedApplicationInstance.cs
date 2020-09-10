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
        IPersistedKeyValueStore PersistedConfiguration { get; }
    }
    
    public class LoadedPersistedApplicationInstance : LoadedApplicationInstance, ILoadedPersistedApplicationInstance
    {
        public LoadedPersistedApplicationInstance(string instanceName, IPersistedKeyValueStore configuration, string configurationPath) : base(instanceName, configuration)
        {
            ConfigurationPath = configurationPath;
            PersistedConfiguration = configuration;
        }

        public IPersistedKeyValueStore PersistedConfiguration { get; }

        public string ConfigurationPath { get; }
    }
}