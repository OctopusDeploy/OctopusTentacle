using System;
using System.Linq;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Manager.Tentacle.TentacleConfiguration
{
    public interface IInstanceConfigurationLoader
    {
        InstanceConfiguration LoadConfiguration(ApplicationInstanceRecord applicationInstanceRecord);
    }

    public class InstanceConfiguration
    {
        public InstanceConfiguration(
            IWritableKeyValueStore keyStore,
            IHomeConfiguration homeConfiguration,
            ILoggingConfiguration loggingConfiguration,
            ITentacleConfiguration tentacleConfiguration)
        {
            KeyStore = keyStore;
            HomeConfiguration = homeConfiguration;
            LoggingConfiguration = loggingConfiguration;
            TentacleConfiguration = tentacleConfiguration;
        }

        public IWritableKeyValueStore KeyStore { get; }
        public IHomeConfiguration HomeConfiguration { get; }
        public ILoggingConfiguration LoggingConfiguration { get; }
        public ITentacleConfiguration TentacleConfiguration { get; }
    }

    public class InstanceConfigurationLoader : IInstanceConfigurationLoader
    {
        readonly IApplicationConfigurationContributor[] instanceStrategies;
        readonly IOctopusFileSystem fileSystem;
        readonly ISystemLog systemLog;

        public InstanceConfigurationLoader(
            IApplicationConfigurationContributor[] instanceStrategies,
            IOctopusFileSystem fileSystem,
            ISystemLog systemLog)
        {
            this.instanceStrategies = instanceStrategies;
            this.fileSystem = fileSystem;
            this.systemLog = systemLog;
        }

        public InstanceConfiguration LoadConfiguration(ApplicationInstanceRecord applicationInstanceRecord)
        {
            var keyStore = new XmlFileKeyValueStore(fileSystem, applicationInstanceRecord.ConfigurationFilePath);

            var selector = new SimpleApplicationInstanceSelectorForTentacleManager(
                instanceStrategies,
                fileSystem,
                systemLog,
                applicationInstanceRecord.ConfigurationFilePath);

            var homeConfig = new HomeConfiguration(selector);
            var loggingConfig = new LoggingConfiguration(homeConfig);
            var tentacleConfig = new Octopus.Tentacle.Configuration.TentacleConfiguration(
                selector,
                homeConfig,
                new ProxyConfiguration(keyStore),
                new PollingProxyConfiguration(keyStore),
                new SystemLog()
            );

            return new InstanceConfiguration(keyStore, homeConfig, loggingConfig, tentacleConfig);
        }

        class SimpleApplicationInstanceSelectorForTentacleManager : IApplicationInstanceSelector
        {
            readonly IApplicationConfigurationContributor[] instanceStrategies;
            readonly IOctopusFileSystem fileSystem;
            readonly ISystemLog log;
            
            readonly string configFilePath;
            readonly bool canLoadCurrentInstance;

            public SimpleApplicationInstanceSelectorForTentacleManager(
                IApplicationConfigurationContributor[] instanceStrategies,
                IOctopusFileSystem fileSystem,
                ISystemLog log,
                string configFilePath)
            {
                this.instanceStrategies = instanceStrategies;
                this.fileSystem = fileSystem;
                this.log = log;
                this.configFilePath = configFilePath;

                try
                {
                    Current = LoadInstance();
                    canLoadCurrentInstance = true;

                }
                catch
                {
                    canLoadCurrentInstance = false;
                }
            }

            public ApplicationName ApplicationName => ApplicationName.Tentacle;
            public ApplicationInstanceConfiguration Current { get; }
            public bool CanLoadCurrentInstance() => canLoadCurrentInstance;

            ApplicationInstanceConfiguration LoadInstance()
            {
                var (aggregatedKeyValueStore, writableConfig) = LoadConfigurationStore(configFilePath);
                return new ApplicationInstanceConfiguration(null, configFilePath, aggregatedKeyValueStore, writableConfig);
            }

            (IKeyValueStore, IWritableKeyValueStore) LoadConfigurationStore(string configurationPath)
            {
                EnsureConfigurationExists(configurationPath);

                log.Verbose($"Loading configuration from {configurationPath}");
                var writable = new XmlFileKeyValueStore(fileSystem, configurationPath);
                return (ContributeAdditionalConfiguration(writable), writable);
            }

            void EnsureConfigurationExists(string configurationPath)
            {
                if (!fileSystem.FileExists(configurationPath ?? string.Empty))
                {
                    var message = $"The configuration file at {configurationPath} could not be located at the specified location.";

                    throw new ControlledFailureException(message);
                }
            }

            AggregatedKeyValueStore ContributeAdditionalConfiguration(IAggregatableKeyValueStore writableConfig)
            {
                // build composite configuration pulling values out of the environment
                var keyValueStores = instanceStrategies
                    .OrderBy(x => x.Priority)
                    .Select(s => s.LoadContributedConfiguration())
                    .WhereNotNull()
                    .ToList();

                // Allow contributed values to override the core writable values.
                keyValueStores.Add(writableConfig);

                return new AggregatedKeyValueStore(keyValueStores.ToArray());
            }
        }
    }
}
