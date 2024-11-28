using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Octopus.Diagnostics;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Kubernetes.Configuration;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Configuration.Instances
{
    class ApplicationInstanceSelector : IApplicationInstanceSelector
    {
        readonly IApplicationInstanceStore applicationInstanceStore;
        readonly StartUpInstanceRequest startUpInstanceRequest;
        readonly IApplicationConfigurationContributor[] instanceStrategies;
        readonly IOctopusFileSystem fileSystem;
        readonly ISystemLog log;
        readonly object @lock = new object();
        ApplicationInstanceConfiguration? current;
        Lazy<ConfigMapKeyValueStore> configMapStoreFactory;

        public ApplicationInstanceSelector(
            ApplicationName applicationName,
            IApplicationInstanceStore applicationInstanceStore,
            StartUpInstanceRequest startUpInstanceRequest,
            IApplicationConfigurationContributor[] instanceStrategies,
            Lazy<ConfigMapKeyValueStore> configMapStoreFactory,
            IOctopusFileSystem fileSystem,
            ISystemLog log)
        {
            this.applicationInstanceStore = applicationInstanceStore;
            this.startUpInstanceRequest = startUpInstanceRequest;
            this.instanceStrategies = instanceStrategies;
            this.fileSystem = fileSystem;
            this.log = log;
            ApplicationName = applicationName;
            this.configMapStoreFactory = configMapStoreFactory;
        }

        public bool CanLoadCurrentInstance()
        {
            try
            {
                LoadCurrentInstance();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public ApplicationInstanceConfiguration Current => LoadCurrentInstance();

        public ApplicationName ApplicationName { get; }

        ApplicationInstanceConfiguration LoadCurrentInstance()
        {
            if (current == null)
            {
                lock (@lock)
                {
                    current ??= LoadInstance();
                }
            }

            return current;
        }

        ApplicationInstanceConfiguration LoadInstance()
        {
            var appInstance = LocateApplicationPrimaryConfiguration();

            var (aggregatedKeyValueStore, writableConfig) = LoadConfigurationStore(appInstance);

            return new ApplicationInstanceConfiguration(appInstance.instanceName, appInstance.configurationpath, aggregatedKeyValueStore, writableConfig);
        }

        (IKeyValueStore, IWritableKeyValueStore) LoadConfigurationStore((string? instanceName, string? configurationpath) appInstance)
        {
            if (appInstance is { instanceName: not null, configurationpath: null } &&
                PlatformDetection.Kubernetes.IsRunningAsKubernetesAgent)
            {
                log.Verbose($"Loading configuration from ConfigMap for namespace {KubernetesConfig.Namespace}");
                var configMapWritableStore = configMapStoreFactory.Value;
                return (ContributeAdditionalConfiguration(configMapWritableStore), configMapWritableStore);
            }

            EnsureConfigurationExists(appInstance.instanceName, appInstance.configurationpath);

            log.Verbose($"Loading configuration from {appInstance.configurationpath}");
            var writable = new XmlFileKeyValueStore(fileSystem, appInstance.configurationpath!);
            return (ContributeAdditionalConfiguration(writable), writable);
        }

        void EnsureConfigurationExists(string? instanceName, string? configurationPath)
        {
            if (!fileSystem.FileExists(configurationPath ?? string.Empty))
            {
                var message = !string.IsNullOrEmpty(instanceName)
                    ? $"The configuration file for instance {instanceName} could not be located at the specified location {configurationPath}. " +
                    "The file might have been manually removed without properly removing the instance and as such it is still listed as present." +
                    "The instance must be created again before you can interact with it."
                    : $"The configuration file at {configurationPath} could not be located at the specified location.";

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

        (string? instanceName, string? configurationpath) LocateApplicationPrimaryConfiguration()
        {
            switch (startUpInstanceRequest)
            {
                case StartUpKubernetesConfigMapInstanceRequest configMapInstanceRequest:
                {   // `--instance` parameter provided and running on Kubernetes.
                    return (configMapInstanceRequest.InstanceName, null);
                }
                case StartUpRegistryInstanceRequest registryInstanceRequest:
                {   //  `--instance` parameter provided. Use That
                    var indexInstance = applicationInstanceStore.LoadInstanceDetails(registryInstanceRequest.InstanceName);
                    return (indexInstance.InstanceName, indexInstance.ConfigurationFilePath);
                }
                case StartUpConfigFileInstanceRequest configFileInstanceRequest:
                {   // `--config` parameter provided. Use that
                    return (null, configFileInstanceRequest.ConfigFile);
                }
                default:
                {   // Look in CWD for config then fallback to Default Named Instance
                    var rootPath = fileSystem.GetFullPath($"{ApplicationName}.config");
                    if (fileSystem.FileExists(rootPath))
                        return (null, rootPath);

                    // This will throw a ControlledFailureException if it can't find the instance so it won't be null
                    var indexDefaultInstance = applicationInstanceStore.LoadInstanceDetails(null);

                    return (indexDefaultInstance.InstanceName, indexDefaultInstance.ConfigurationFilePath);
                }
            }
        }
    }
}