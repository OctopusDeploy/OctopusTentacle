using System;
using System.Linq;
using Octopus.Configuration;
using Octopus.CoreUtilities.Extensions;
using Octopus.Shared.Startup;

namespace Octopus.Shared.Configuration.Instances
{
    class ApplicationInstanceSelector : IApplicationInstanceSelector
    {
        readonly StartUpInstanceRequest startUpInstanceRequest;
        readonly IApplicationConfigurationStrategy[] instanceStrategies;
        readonly ILogFileOnlyLogger logFileOnlyLogger;
        readonly object @lock = new object();
        (string? InstanceName, IKeyValueStore Configuration, IWritableKeyValueStore WritableConfiguration, bool CanRunAsService)? current;

        public ApplicationInstanceSelector(StartUpInstanceRequest startUpInstanceRequest,
            IApplicationConfigurationStrategy[] instanceStrategies,
            ILogFileOnlyLogger logFileOnlyLogger)
        {
            this.startUpInstanceRequest = startUpInstanceRequest;
            this.instanceStrategies = instanceStrategies;
            this.logFileOnlyLogger = logFileOnlyLogger;
        }

        public ApplicationName ApplicationName => startUpInstanceRequest.ApplicationName;

        public bool CanLoadCurrentInstance()
        {
            try
            {
                GetCurrentName();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string? GetCurrentName()
        {
            return LoadCurrentInstance().InstanceName;
        }

        public IKeyValueStore GetCurrentConfiguration()
        {
            return LoadCurrentInstance().Configuration;
        }

        public IWritableKeyValueStore GetWritableCurrentConfiguration()
        {
            return LoadCurrentInstance().WritableConfiguration;
        }

        public bool CanRunAsService()
        {
            return LoadCurrentInstance().CanRunAsService;
        }
        
        (string? InstanceName, IKeyValueStore Configuration, IWritableKeyValueStore WritableConfiguration, bool CanRunAsService) LoadCurrentInstance()
        {
            if (current == null)
                lock (@lock)
                {
                    if (current == null)
                    {
                        current = LoadInstance();
                        var homeConfig = new HomeConfiguration(startUpInstanceRequest.ApplicationName, current.Value.Configuration);
                        // BEWARE if you try to resolve HomeConfiguration from the container you'll create a loop
                        // back to here
                        var logInit = new LogInitializer(new LoggingConfiguration(homeConfig), logFileOnlyLogger);
                        logInit.Start();
                    }
                }

            return current.Value;
        }

        internal (string? InstanceName, IKeyValueStore Configuration, IWritableKeyValueStore WritableConfiguration, bool CanRunAsService) LoadInstance()
        {
            IWritableKeyValueStore? writableConfiguration = null;
            string? instanceName = null;
            bool canRunAsService = false;
            // build a composite configuration
            var keyValueStores = instanceStrategies
                .OrderBy(x => x.Priority)
                .Select(s =>
                {
                    ApplicationRecord? record;
                    if (s is IApplicationConfigurationWithMultipleInstances multipleInstances)
                    {
                        ApplicationInstanceRecord? persistedRecord;
                        if (startUpInstanceRequest is StartUpRegistryInstanceRequest persistedRequest)
                        {
                            persistedRecord = GetNamedRegistryRecord(persistedRequest.InstanceName, multipleInstances);
                            if (persistedRecord == null)
                                throw new ControlledFailureException($"Instance {persistedRequest.InstanceName} of {startUpInstanceRequest.ApplicationName} has not been configured on this machine. Please pass --instance=INSTANCENAME when invoking this command to target a specific instance. Available instances: {AvailableInstances(multipleInstances)}.");
                        }
                        else
                        {
                            // No `--instance` provided, try get Default
                            persistedRecord = GetDefaultRegistryRecord(multipleInstances);
                            if (persistedRecord == null)
                                return null;

                            canRunAsService = true;
                        }

                        logFileOnlyLogger.Info($"Using config from {persistedRecord.ConfigurationFilePath}");

                        record = persistedRecord;
                        instanceName = persistedRecord.InstanceName;
                    }
                    else
                    {
                        record = new ApplicationRecord();
                    }

                    var keyValueStore = s.LoadedConfiguration(record);
                    if (keyValueStore == null)
                        return null;

                    if (s is WorkingDirectoryConfigurationStrategy)
                    {
                        canRunAsService = true;
                    }
                    
                    if (writableConfiguration == null && keyValueStore is IWritableKeyValueStore writableKeyValueStore)
                        writableConfiguration = writableKeyValueStore;

                    return keyValueStore;
                })
                .WhereNotNull()
                .ToArray();

            if (!keyValueStores.Any())
                throw new ControlledFailureException(
                    $"There are no instances of {startUpInstanceRequest.ApplicationName} configured on this machine. Please run the setup wizard, configure an instance using the command-line interface, specify a configuration file, or set the required environment variables.");

            var aggregatedKeyValueStore = new AggregatedKeyValueStore(keyValueStores);
            writableConfiguration ??= new DoNotAllowWritesInThisModeKeyValueStore(aggregatedKeyValueStore);

            return (instanceName, aggregatedKeyValueStore, writableConfiguration, canRunAsService);
        }

        private ApplicationInstanceRecord? GetNamedRegistryRecord(string instanceName, IApplicationConfigurationWithMultipleInstances multipleInstances)
        {
            var persistedApplicationInstanceRecords = multipleInstances.ListInstances();
         
            var possibleNamedInstances = persistedApplicationInstanceRecords
                .Where(i => string.Equals(i.InstanceName, instanceName, StringComparison.InvariantCultureIgnoreCase))
                .ToArray();

            if (possibleNamedInstances.Length == 0)
                throw new ControlledFailureException($"Instance {instanceName} of {startUpInstanceRequest.ApplicationName} has not been configured on this machine. Available instances: {AvailableInstances(multipleInstances)}.");

            if (possibleNamedInstances.Length == 1 && possibleNamedInstances.Count() == 1)
            {
                var applicationInstanceRecord = possibleNamedInstances.First();
                return applicationInstanceRecord;
            }
            else
            {
                // to get more than 1, there must have been a match on differing case, try an exact case match
                var exactMatch = possibleNamedInstances.FirstOrDefault(x => x.InstanceName == instanceName);
                if (exactMatch == null) // null here means all matches were different case
                    throw new ControlledFailureException($"Instance {instanceName} of {startUpInstanceRequest.ApplicationName} could not be matched to one of the existing instances: {AvailableInstances(multipleInstances)}.");
                return exactMatch;
            }

        }

        ApplicationInstanceRecord? GetDefaultRegistryRecord(IApplicationConfigurationWithMultipleInstances multipleInstances)
        {
            var persistedApplicationInstanceRecords = multipleInstances.ListInstances();
            // pick the default, if there is one
            var defaultInstance = persistedApplicationInstanceRecords.FirstOrDefault(i => i.InstanceName == ApplicationInstanceRecord.GetDefaultInstance(ApplicationName));
            if (defaultInstance != null)
            {
                return defaultInstance;
            }

            // if there is only a single instance, then pick it
            if (persistedApplicationInstanceRecords.Count == 1)
            {
                var singleInstance = persistedApplicationInstanceRecords.Single();
                return singleInstance;
            }
            
            if (persistedApplicationInstanceRecords.Count > 1)
                throw new ControlledFailureException($"There is more than one instance of {startUpInstanceRequest.ApplicationName} configured on this machine. Please pass --instance=INSTANCENAME when invoking this command to target a specific instance. Available instances: {AvailableInstances(multipleInstances)}.");

            return null;
        }
        
        string AvailableInstances(IApplicationConfigurationWithMultipleInstances multipleInstances)
        {
            return string.Join(", ",
                multipleInstances.ListInstances()
                    .OrderBy(x => x.InstanceName, StringComparer.InvariantCultureIgnoreCase)
                    .Select(x => x.InstanceName));
        }
    }
}