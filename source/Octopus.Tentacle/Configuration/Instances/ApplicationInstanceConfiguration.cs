using System;

namespace Octopus.Tentacle.Configuration.Instances
{
    public class ApplicationInstanceConfiguration {
        public ApplicationInstanceConfiguration(
            string? instanceName,
            string? configurationPath,
            IKeyValueStore? configuration,
            IWritableKeyValueStore? writableConfiguration)
        {
            InstanceName = instanceName;
            ConfigurationPath = configurationPath;
            Configuration = configuration;
            WritableConfiguration = writableConfiguration;
        }

        public string? ConfigurationPath { get;  }
        public string? InstanceName { get;  }

        public IKeyValueStore? Configuration { get; }

        public IWritableKeyValueStore? WritableConfiguration { get; }
    }
}