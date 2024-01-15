using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Octopus.Configuration;
using Octopus.Diagnostics;
using Octopus.Tentacle.Util;
#if !NETFRAMEWORK
using k8s;
using k8s.Models;
using Newtonsoft.Json;
#endif

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
        
        public ApplicationInstanceSelector(
            ApplicationName applicationName,
            IApplicationInstanceStore applicationInstanceStore,
            StartUpInstanceRequest startUpInstanceRequest,
            IApplicationConfigurationContributor[] instanceStrategies,
            IOctopusFileSystem fileSystem,
            ISystemLog log)
        {
            this.applicationInstanceStore = applicationInstanceStore;
            this.startUpInstanceRequest = startUpInstanceRequest;
            this.instanceStrategies = instanceStrategies;
            this.fileSystem = fileSystem;
            this.log = log;
            ApplicationName = applicationName;
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

            IKeyValueStore aggregatedKeyValueStore;
            IWritableKeyValueStore writableConfig;
#if !NETFRAMEWORK
            if (appInstance is { instanceName: not null, configurationpath: null })
            {
                Console.WriteLine($"Adding ConfigMap Key Value Store: instanceName: {appInstance.instanceName}");
                var writeable = new ConfigMapKeyValueStore(appInstance.instanceName);
                writableConfig = writeable;
                aggregatedKeyValueStore = ContributeAdditionalConfiguration(writeable);
            }
            else
            {
#endif
                if (!ConfigurationFileExists(appInstance.configurationpath))
                {
                    ThrowConfigurationMissingException(appInstance.instanceName, appInstance.configurationpath);
                }

                log.Verbose($"Loading configuration from {appInstance.configurationpath}");
                var writeable = new XmlFileKeyValueStore(fileSystem, appInstance.configurationpath!);
                writableConfig = writeable;
                aggregatedKeyValueStore = ContributeAdditionalConfiguration(writeable);
#if !NETFRAMEWORK
            }
#endif

            return new ApplicationInstanceConfiguration(appInstance.instanceName, appInstance.configurationpath, aggregatedKeyValueStore, writableConfig);
        }

        bool ConfigurationFileExists([NotNullWhen(true)] string? configurationPath)
        {
            return fileSystem.FileExists(configurationPath ?? "");
        }

        void ThrowConfigurationMissingException(string? instanceName, string? configurationPath)
        {
            var message = !string.IsNullOrEmpty(instanceName)
                ? $"The configuration file for instance {instanceName} could not be located at the specified location {configurationPath}. " +
                "The file might have been manually removed without properly removing the instance and as such it is still listed as present." +
                "The instance must be created again before you can interact with it."
                : $"The configuration file at {configurationPath} could not be located at the specified location.";

            throw new ControlledFailureException(message);
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

#if !NETFRAMEWORK
    class ConfigMapKeyValueStore : IWritableKeyValueStore, IAggregatableKeyValueStore
    {
        const string Namespace = "octopus";
        readonly string name;
        readonly k8s.Kubernetes client;

        V1ConfigMap configMap;

        IDictionary<string, string> ConfigMapData => configMap.Data ?? (configMap.Data = new Dictionary<string, string>());

        public ConfigMapKeyValueStore(string instanceName)
        {
            var kubeConfig = KubernetesClientConfiguration.InClusterConfig();
            client = new k8s.Kubernetes(kubeConfig);
            name = $"{instanceName.ToLowerInvariant()}-configmap";
            V1ConfigMap? config;
            try
            {
                config = client.CoreV1.ReadNamespacedConfigMap(name, Namespace);
            }
            catch (Exception e)
            {
                Console.WriteLine($"ConfigMapKeyValueStore.ctor Exception: {e}");
                config = null;
            }

            configMap = config ?? client.CoreV1.CreateNamespacedConfigMap(new V1ConfigMap { Metadata = new V1ObjectMeta { Name = name, NamespaceProperty = Namespace }}, Namespace);
            Console.WriteLine($"ConfigMapKeyValueStore.ctor ConfigMap loaded: {JsonConvert.SerializeObject(configMap)}");
        }

        public string? Get(string name, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            return ConfigMapData.TryGetValue(name, out var value) ? value : null;
        }

        public TData? Get<TData>(string name, TData? defaultValue = default, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            var result = TryGet<TData>(name, protectionLevel);

            return result.foundResult ? result.value : defaultValue;
        }

        public bool Set(string name, string? value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            configMap.Data[name] = value;
            return Save();
        }

        public bool Set<TData>(string name, TData value, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            return Set(name, JsonConvert.SerializeObject(value), protectionLevel);
        }

        public bool Remove(string name)
        {
            return ConfigMapData.Remove(name) && Save();
        }

        public bool Save()
        {
            configMap = client.CoreV1.ReplaceNamespacedConfigMap(configMap, name, Namespace);
            return true;
        }

        public (bool foundResult, TData? value) TryGet<TData>(string name, ProtectionLevel protectionLevel = ProtectionLevel.None)
        {
            var value = Get(name, protectionLevel);

            if (value == null) return (false, default);

            try
            {
                return value is TData data ? (true, data) : (true, JsonConvert.DeserializeObject<TData>(value));
            }
            catch (Exception e)
            {
                Console.WriteLine($"ConfigMapKeyValueStore.TryGet<TData>: Exception! {e}");
                return (false, default);
            }
        }
    }
#endif
}