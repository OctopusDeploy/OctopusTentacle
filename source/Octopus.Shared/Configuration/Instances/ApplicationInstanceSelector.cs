using System;
using System.Linq;
using Octopus.Configuration;
using Octopus.Shared.Startup;

namespace Octopus.Shared.Configuration.Instances
{
    class ApplicationInstanceSelector : IApplicationInstanceSelector
    {
        readonly StartUpInstanceRequest startUpInstanceRequest;
        readonly IApplicationConfigurationStrategy[] instanceStrategies;
        readonly ILogFileOnlyLogger logFileOnlyLogger;
        readonly object @lock = new object();
        (string? InstanceName, IKeyValueStore Configuration, IWritableKeyValueStore WritableConfiguration)? current;

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

        public bool IsCurrentInstanceDefault()
        {
            if (current == null)
            {
                lock (@lock)
                {
                    if (current == null)
                        current = LoadCurrentInstance();
                }
            }
            return current?.InstanceName == ApplicationName.ToString();
        }

        public string? GetCurrentName()
        {
            if (current == null)
            {
                lock (@lock)
                {
                    if (current == null)
                        current = LoadCurrentInstance();
                }
            }
            return current?.InstanceName;
        }

        public IKeyValueStore GetCurrentConfiguration()
        {
            if (current == null)
            {
                lock (@lock)
                {
                    if (current == null)
                        current = LoadCurrentInstance();
                }
            }
            return current.Value.Configuration;
        }

        public IWritableKeyValueStore GetWritableCurrentConfiguration()
        {
            if (current == null)
            {
                lock (@lock)
                {
                    if (current == null)
                        current = LoadCurrentInstance();
                }
            }
            return current.Value.WritableConfiguration;
        }

        (string? InstanceName, IKeyValueStore Configuration, IWritableKeyValueStore WritableConfiguration) LoadCurrentInstance()
        {
            var instance = LoadInstance();

            var homeConfig = new HomeConfiguration(startUpInstanceRequest.ApplicationName, instance.Configuration);

            // BEWARE if you try to resolve HomeConfiguration from the container you'll create a loop
            // back to here
            var logInit = new LogInitializer(new LoggingConfiguration(homeConfig), logFileOnlyLogger);
            logInit.Start();

            return instance;
        }

        internal (string? InstanceName, IKeyValueStore Configuration, IWritableKeyValueStore WritableConfiguration) LoadInstance()
        {
            string? instanceName = null;
            IWritableKeyValueStore? writableConfiguration = null;

            // build a composite configuration
            var keyValueStores = instanceStrategies
                .OrderBy(x => x.Priority)
                .Select(s =>
                {
                    ApplicationRecord? record = null;

                    if (s is IApplicationConfigurationWithMultipleInstances multipleInstances)
                    {
                        var persistedApplicationInstanceRecords = multipleInstances.ListInstances();

                        if (startUpInstanceRequest is StartUpPersistedInstanceRequest persistedRequest)
                        {
                            instanceName = persistedRequest.InstanceName;

                            var possibleNamedInstances = persistedApplicationInstanceRecords
                                .Where(i => string.Equals(i.InstanceName, persistedRequest.InstanceName, StringComparison.InvariantCultureIgnoreCase))
                                .ToArray();

                            if (possibleNamedInstances.Length == 0)
                                throw new ControlledFailureException($"Instance {persistedRequest.InstanceName} of {persistedRequest.ApplicationName} has not been configured on this machine. Available instances: {AvailableInstances(multipleInstances)}.");

                            if (possibleNamedInstances.Length == 1 && possibleNamedInstances.Count() == 1)
                            {
                                var applicationInstanceRecord = possibleNamedInstances.First();
                                instanceName = applicationInstanceRecord.InstanceName;
                                record = applicationInstanceRecord;
                            }
                            else
                            {
                                // to get more than 1, there must have been a match on differing case, try an exact case match
                                var exactMatch = possibleNamedInstances.FirstOrDefault(x => x.InstanceName == persistedRequest.InstanceName);
                                if (exactMatch == null) // null here means all matches were different case
                                    throw new ControlledFailureException($"Instance {persistedRequest.InstanceName} of {persistedRequest.ApplicationName} could not be matched to one of the existing instances: {AvailableInstances(multipleInstances)}.");
                                instanceName = exactMatch.InstanceName;
                                record = exactMatch;
                            }

                            if (record == null)
                                throw new ControlledFailureException($"Instance {persistedRequest.InstanceName} of {startUpInstanceRequest.ApplicationName} has not been configured on this machine. Please pass --instance=INSTANCENAME when invoking this command to target a specific instance. Available instances: {AvailableInstances(multipleInstances)}.");
                        }
                        else
                        {
                            // pick the default, if there is one
                            var defaultInstance = persistedApplicationInstanceRecords.FirstOrDefault(i => i.InstanceName == ApplicationInstanceRecord.GetDefaultInstance(ApplicationName));
                            if (defaultInstance != null)
                            {
                                instanceName = defaultInstance.InstanceName;
                                record = defaultInstance;
                            }

                            // if there is only a single instance, then pick it
                            if (persistedApplicationInstanceRecords.Count == 1)
                            {
                                var singleInstance = persistedApplicationInstanceRecords.Single();
                                instanceName = singleInstance.InstanceName;
                                record = singleInstance;
                            }

                            if (record == null && persistedApplicationInstanceRecords.Count > 1)
                                throw new ControlledFailureException($"There is more than one instance of {startUpInstanceRequest.ApplicationName} configured on this machine. Please pass --instance=INSTANCENAME when invoking this command to target a specific instance. Available instances: {AvailableInstances(multipleInstances)}.");

                            if (record == null)
                                return null;
                        }
                    }
                    else
                        record = new ApplicationRecord();

                    var keyValueStore = s.LoadedConfiguration(record);
                    if (writableConfiguration == null && keyValueStore is IWritableKeyValueStore writableKeyValueStore)
                    {
                        writableConfiguration = writableKeyValueStore;
                    }

                    if (record is ApplicationInstanceRecord persistedRecord)
                        logFileOnlyLogger.Info($"Using config from {persistedRecord.ConfigurationFilePath}");

                    return keyValueStore;
                })
                .Where(x => x != null)
                .Cast<IKeyValueStore>()
                .ToArray();

            if (!keyValueStores.Any())
                throw new ControlledFailureException(
                    $"There are no instances of {startUpInstanceRequest.ApplicationName} configured on this machine. Please run the setup wizard, configure an instance using the command-line interface, specify a configuration file, or set the required environment variables.");

            var aggregatedKeyValueStore = new AggregatedKeyValueStore(keyValueStores);

            if (writableConfiguration == null)
                writableConfiguration = new DoNotAllowWritesInThisModeKeyValueStore(aggregatedKeyValueStore);

            return (instanceName, aggregatedKeyValueStore, writableConfiguration);
        }

        string AvailableInstances(IApplicationConfigurationWithMultipleInstances multipleInstances)
        {
            return string.Join(", ", multipleInstances.ListInstances()
                    .OrderBy(x => x.InstanceName, StringComparer.InvariantCultureIgnoreCase)
                    .Select(x => x.InstanceName));
        }
    }
}