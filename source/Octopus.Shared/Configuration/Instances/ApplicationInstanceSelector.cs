using System;
using System.Linq;
using Octopus.Configuration;
using Octopus.Diagnostics;
using Octopus.Shared.Startup;

namespace Octopus.Shared.Configuration.Instances
{
    public class ApplicationInstanceSelector : IApplicationInstanceSelector
    {
        readonly ILog log;
        readonly StartUpInstanceRequest startUpInstanceRequest;
        readonly IApplicationConfigurationStrategy[] instanceStrategies;
        readonly ILogFileOnlyLogger logFileOnlyLogger;
        readonly object @lock = new object();
        (string? InstanceName, IKeyValueStore Configuration)? current;

        public ApplicationInstanceSelector(ILog log,
            StartUpInstanceRequest startUpInstanceRequest,
            IApplicationConfigurationStrategy[] instanceStrategies,
            ILogFileOnlyLogger logFileOnlyLogger)
        {
            this.log = log;
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

        (string? InstanceName, IKeyValueStore Configuration) LoadCurrentInstance()
        {
            var instance = LoadInstance();

            var homeConfig = new HomeConfiguration(startUpInstanceRequest.ApplicationName, instance.Configuration);

            // BEWARE if you try to resolve HomeConfiguration from the container you'll create a loop
            // back to here
            var logInit = new LogInitializer(new LoggingConfiguration(homeConfig), logFileOnlyLogger);
            logInit.Start();

            return instance;
        }

        internal (string? InstanceName, IKeyValueStore Configuration) LoadInstance()
        {
            string? instanceName = null;

            // build a composite configuration
            var keyValueStores = instanceStrategies.Select(s =>
                {
                    ApplicationInstanceRecord? record = null;

                    if (s is IApplicationConfigurationWithMultipleInstances multipleInstances)
                    {
                        var persistedApplicationInstanceRecords = multipleInstances.ListInstances();

                        if (persistedApplicationInstanceRecords.Count == 0)
                            throw new ControlledFailureException(
                                $"There are no instances of {startUpInstanceRequest.ApplicationName} configured on this machine. Please run the setup wizard, configure an instance using the command-line interface, specify a configuration file, or set the required environment variables.");

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
                                record = new PersistedApplicationInstanceRecord(instanceName, applicationInstanceRecord.ConfigurationFilePath, instanceName == ApplicationName.ToString());
                            }
                            else
                            {
                                // to get more than 1, there must have been a match on differing case, try an exact case match
                                var exactMatch = possibleNamedInstances.FirstOrDefault(x => x.InstanceName == persistedRequest.InstanceName);
                                if (exactMatch == null) // null here means all matches were different case
                                    throw new ControlledFailureException($"Instance {persistedRequest.InstanceName} of {persistedRequest.ApplicationName} could not be matched to one of the existing instances: {AvailableInstances(multipleInstances)}.");
                                instanceName = exactMatch.InstanceName;
                                record = new PersistedApplicationInstanceRecord(exactMatch.InstanceName, exactMatch.ConfigurationFilePath, exactMatch.InstanceName == ApplicationName.ToString());
                            }

                            if (record == null)
                                throw new ControlledFailureException($"Instance {persistedRequest.InstanceName} of {startUpInstanceRequest.ApplicationName} has not been configured on this machine. Please pass --instance=INSTANCENAME when invoking this command to target a specific instance. Available instances: {AvailableInstances(multipleInstances)}.");
                        }
                        else
                        {
                            // pick the default, if there is one
                            var defaultInstance = persistedApplicationInstanceRecords.FirstOrDefault(i => i.IsDefaultInstance);
                            if (defaultInstance != null)
                            {
                                instanceName = defaultInstance.InstanceName;
                                record = new PersistedApplicationInstanceRecord(defaultInstance.InstanceName, defaultInstance.ConfigurationFilePath, true);
                            }

                            // if there is only a single instance, then pick it
                            if (persistedApplicationInstanceRecords.Count() == 1)
                            {
                                var singleInstance = persistedApplicationInstanceRecords.Single();
                                instanceName = singleInstance.InstanceName;
                                record = new PersistedApplicationInstanceRecord(singleInstance.InstanceName, singleInstance.ConfigurationFilePath, singleInstance.InstanceName == ApplicationName.ToString());
                            }

                            if (record == null)
                                throw new ControlledFailureException($"There is more than one instance of {startUpInstanceRequest.ApplicationName} configured on this machine. Please pass --instance=INSTANCENAME when invoking this command to target a specific instance. Available instances: {AvailableInstances(multipleInstances)}.");
                        }
                    }
                    else
                        record = new ApplicationInstanceRecord();

                    return s.LoadedConfiguration(record);
                })
                .Where(x => x != null)
                .Cast<IKeyValueStore>()
                .ToArray();

            if (!keyValueStores.Any())
                throw new ControlledFailureException(
                    $"There are no instances of {startUpInstanceRequest.ApplicationName} configured on this machine. Please run the setup wizard, configure an instance using the command-line interface, specify a configuration file, or set the required environment variables.");

            return (instanceName, new AggregatedKeyValueStore(log, keyValueStores));
        }

        string AvailableInstances(IApplicationConfigurationWithMultipleInstances multipleInstances)
        {
            return string.Join(", ", multipleInstances.ListInstances()
                    .OrderBy(x => x.InstanceName, StringComparer.InvariantCultureIgnoreCase)
                    .Select(x => x.InstanceName));
        }
    }
}