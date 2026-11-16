using System;

namespace Octopus.Tentacle.Configuration.Instances
{
    public class ApplicationInstanceConfiguration {
        public ApplicationInstanceConfiguration(
            string? instanceName,
            string? configurationPath,
            IKeyValueStore? configuration,
            IWritableKeyValueStore? writableConfiguration,
            string? keyFilePath = null)
        {
            KeyFilePath = keyFilePath;
            InstanceName = instanceName;
            ConfigurationPath = configurationPath;
            Configuration = configuration;
            WritableConfiguration = writableConfiguration;
        }

        public string? ConfigurationPath { get;  }
        public string? InstanceName { get;  }

        // The Linux configuration encryption key, or null when the instance has none.
        public string? KeyFilePath { get; }

        public IKeyValueStore? Configuration { get; }

        public IWritableKeyValueStore? WritableConfiguration { get; }
    }
}