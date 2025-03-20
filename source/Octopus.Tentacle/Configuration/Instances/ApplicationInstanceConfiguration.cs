using System;

namespace Octopus.Tentacle.Configuration.Instances
{
    public class ApplicationInstanceConfiguration : IDisposable
    {
        public ApplicationInstanceConfiguration(
            string? instanceName,
            string? configurationPath,
            IKeyValueStore? configuration,
            IWritableKeyValueStore? writableConfiguration,
            Func<IKeyValueStore?>? loadConfigurationFunction)
        {
            InstanceName = instanceName;
            ConfigurationPath = configurationPath;
            Configuration = configuration;
            WritableConfiguration = writableConfiguration;
            
            if (configuration != null)
            {
                ChangeDetectingConfiguration = new ChangeDetectingKeyValueStore(configuration, configurationPath, loadConfigurationFunction);
            }
        }

        public string? ConfigurationPath { get;  }
        public string? InstanceName { get;  }

        public IKeyValueStore? Configuration { get; }

        public ChangeDetectingKeyValueStore? ChangeDetectingConfiguration { get; }

        public IWritableKeyValueStore? WritableConfiguration { get; }

        public void Dispose()
        {
            ChangeDetectingConfiguration?.Dispose();
        }
    }
}