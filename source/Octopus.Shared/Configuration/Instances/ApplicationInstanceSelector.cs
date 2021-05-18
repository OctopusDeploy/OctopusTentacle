using System;
using System.Linq;
using Octopus.CoreUtilities.Extensions;
using Octopus.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration.Instances
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
            EnsureConfigurationExists(appInstance.instanceName, appInstance.configurationpath);

            log.Verbose($"Loading configuration from {appInstance.configurationpath}");
            var writableConfig = new XmlFileKeyValueStore(fileSystem, appInstance.configurationpath);
            
            var aggregatedKeyValueStore = ContributeAdditionalConfiguration(writableConfig);
            return new ApplicationInstanceConfiguration(appInstance.instanceName, appInstance.configurationpath, aggregatedKeyValueStore, writableConfig);
        }

        void EnsureConfigurationExists(string? instanceName, string configurationPath)
        {
            if (!fileSystem.FileExists(configurationPath))
            {
                var message = !string.IsNullOrEmpty(instanceName)
                    ? $"The configuration file for instance {instanceName} was unable to be located at the specified location {configurationPath}. " +
                    "The file might have been manually removed without properly removing the instance and as such it is still listed as present." +
                    "The instance must be created again before you can interact with it."
                    : $"The configuration file at {configurationPath} was unable to be located at the specified location.";
                    
                throw new ControlledFailureException(message);
            }
        }

        AggregatedKeyValueStore ContributeAdditionalConfiguration(XmlFileKeyValueStore writableConfig)
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

        (string? instanceName, string configurationpath) LocateApplicationPrimaryConfiguration()
        {
            switch (startUpInstanceRequest)
            {
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

                    if (!applicationInstanceStore.TryLoadInstanceDetails(null, out var indexDefaultInstance))
                    {
                        throw new ControlledFailureException("There are no instances of OctopusServer configured on this machine. " +
                            "Please run the setup wizard, configure an instance using the command-line interface or specify a configuration file");
                    }

                    return (indexDefaultInstance!.InstanceName, indexDefaultInstance!.ConfigurationFilePath);
                }
            }
        }
    }
}