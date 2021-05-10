using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Octopus.Configuration;
using Octopus.CoreUtilities.Extensions;
using Octopus.Shared.Startup;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration.Instances
{
    public class ApplicationInstanceConfiguration {
        public ApplicationInstanceConfiguration(string? instanceName,
            string configurationPath,
            IKeyValueStore configuration,
            IWritableKeyValueStore writableConfiguration)
        {
            InstanceName = instanceName;
            ConfigurationPath = configurationPath;
            Configuration = configuration;
            WritableConfiguration = writableConfiguration;
        }

        public string ConfigurationPath { get;  }
        public string? InstanceName { get;  }

        public IKeyValueStore Configuration { get; }

        public IWritableKeyValueStore WritableConfiguration { get; }
    }

    
    class ApplicationInstanceSelector : IApplicationInstanceSelector
    {
        readonly IApplicationInstanceStore applicationInstanceStore;
        readonly StartUpInstanceRequest startUpInstanceRequest;
        readonly IApplicationConfigurationStrategy[] instanceStrategies;
        readonly ILogFileOnlyLogger logFileOnlyLogger;
        readonly IOctopusFileSystem fileSystem;
        readonly object @lock = new object();
        ApplicationInstanceConfiguration? current;
        
        public ApplicationInstanceSelector(
            IApplicationInstanceStore applicationInstanceStore,
            StartUpInstanceRequest startUpInstanceRequest,
            IApplicationConfigurationStrategy[] instanceStrategies,
            ILogFileOnlyLogger logFileOnlyLogger,
            IOctopusFileSystem fileSystem,
            ApplicationName applicationName)
        {
            this.applicationInstanceStore = applicationInstanceStore;
            this.startUpInstanceRequest = startUpInstanceRequest;
            this.instanceStrategies = instanceStrategies;
            this.logFileOnlyLogger = logFileOnlyLogger;
            this.fileSystem = fileSystem;
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
                lock (@lock)
                {
                    if (current == null)
                    {
                        current = LoadInstance();
                        InitializeLogging();
                    }
                }
            return current;
        }

        void InitializeLogging()
        {
            Debug.Assert(current != null, nameof(current) + " != null");
            var homeConfig = new HomeConfiguration(startUpInstanceRequest.ApplicationName, current.Configuration, this);
            // BEWARE if you try to resolve HomeConfiguration from the container you'll create a loop
            // back to here
            var logInit = new LogInitializer(new LoggingConfiguration(homeConfig), logFileOnlyLogger);
            logInit.Start();
        }

        ApplicationInstanceConfiguration LoadInstance()
        {
            var appInstance = LocateApplicationPrimaryConfiguration();
            var writableConfig = new XmlFileKeyValueStore(fileSystem, appInstance.configurationpath);
            
            var aggregatedKeyValueStore = ContributeAdditionalConfiguration(writableConfig);
            return new ApplicationInstanceConfiguration(appInstance.instanceName, appInstance.configurationpath, aggregatedKeyValueStore, writableConfig);
        }

        AggregatedKeyValueStore ContributeAdditionalConfiguration(XmlFileKeyValueStore writableConfig)
        {
            // build composite configuration pulling values out of the environment
            var keyValueStores = instanceStrategies
                .OrderBy(x => x.Priority)
                .Select(s => s.LoadContributedConfiguration())
                .WhereNotNull()
                .ToArray();

            var aggregatedKeyValues = new IAggregatableKeyValueStore[] {writableConfig};
            aggregatedKeyValues.AddRange(keyValueStores);
            
            return new AggregatedKeyValueStore(aggregatedKeyValues);
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
                    if (!fileSystem.FileExists(configFileInstanceRequest.ConfigFile))
                        throw new FileNotFoundException($"Specified config file {configFileInstanceRequest.ConfigFile} not found.");
                    return (null, configFileInstanceRequest.ConfigFile);
                }
                default:
                {   // Look in CWD for config then fallback to Default Named Instance
                    var rootPath = Path.Combine(Environment.CurrentDirectory, $"{ApplicationName}.config");
                    if (fileSystem.FileExists(rootPath))
                        return (null, rootPath);
                    var indexInstanceFallback = applicationInstanceStore.LoadInstanceDetails(null);
                    return (indexInstanceFallback.InstanceName, indexInstanceFallback.ConfigurationFilePath);
                }
            }
        }
    }
}