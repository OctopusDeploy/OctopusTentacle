using System;
using System.Linq;
using Octopus.Configuration;
using Octopus.Shared.Startup;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration.Instances
{
    class ApplicationInstanceSelector : IApplicationInstanceSelector
    {
        readonly string currentInstanceName;
        readonly IOctopusFileSystem fileSystem;
        readonly IApplicationInstanceStore instanceStore;
        readonly ILogFileOnlyLogger logFileOnlyLogger;
        readonly object @lock = new object();
        (string? InstanceName, IKeyValueStore Configuration, IWritableKeyValueStore WritableConfiguration)? current;

        public ApplicationInstanceSelector(ApplicationName applicationName,
            string currentInstanceName,
            IOctopusFileSystem fileSystem,
            IApplicationInstanceStore instanceStore,
            ILogFileOnlyLogger logFileOnlyLogger)
        {
            ApplicationName = applicationName;
            this.currentInstanceName = currentInstanceName;
            this.fileSystem = fileSystem;
            this.instanceStore = instanceStore;
            this.logFileOnlyLogger = logFileOnlyLogger;
        }

        public ApplicationName ApplicationName { get; }

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

            // BEWARE if you try to resolve HomeConfiguration from the container you'll create a loop
            // back to here
            var homeConfig = new HomeConfiguration(ApplicationName, instance.Configuration);
            var logInit = new LogInitializer(new LoggingConfiguration(homeConfig), logFileOnlyLogger);
            logInit.Start();

            return instance;
        }

        internal (string? InstanceName, IKeyValueStore Configuration, IWritableKeyValueStore WritableConfiguration) LoadInstance()
        {
            ApplicationInstanceRecord? instance = null;
            var anyInstances = instanceStore.AnyInstancesConfigured();
            if (!anyInstances)
                throw new ControlledFailureException(
                    $"There are no instances of {ApplicationName} configured on this machine. Please run the setup wizard or configure an instance using the command-line interface.");

            instance = string.IsNullOrEmpty(currentInstanceName) ? TryLoadDefaultInstance() : TryLoadInstanceByName();

            if (instance == null)
            {
                var instances = instanceStore.ListInstances();
                throw new ControlledFailureException(
                    $"Instance {currentInstanceName} of {ApplicationName} has not been configured on this machine. Available instances: {string.Join(", ", instances.Select(i => i.InstanceName))}.");
            }

            instanceStore.MigrateInstance(instance);
            var store = new XmlFileKeyValueStore(fileSystem, instance.ConfigurationFilePath);
            return (instance.InstanceName, store, store);
        }

        ApplicationInstanceRecord? TryLoadInstanceByName()
        {
            var instance = instanceStore.GetInstance(currentInstanceName);
            if (instance == null)
            {
                var instances = instanceStore.ListInstances();
                var caseInsensitiveMatches = instances.Where(s => string.Equals(s.InstanceName, currentInstanceName, StringComparison.InvariantCultureIgnoreCase)).ToArray();
                if (caseInsensitiveMatches.Length > 1)
                    throw new ControlledFailureException(
                        $"Instance {currentInstanceName} of {ApplicationName} could not be matched to one of the existing instances: {string.Join(", ", instances.Select(i => i.InstanceName))}.");
                if (caseInsensitiveMatches.Length == 1)
                {
                    instance = caseInsensitiveMatches.First();
                }
            }

            return instance;
        }

        ApplicationInstanceRecord TryLoadDefaultInstance()
        {
            ApplicationInstanceRecord instance;
            var instances = instanceStore.ListInstances();
            if (instances.Count == 1)
            {
                instance = instances.First();
            }
            else
            {
                var defaultInstance = instances.FirstOrDefault(s => string.Equals(s.InstanceName, ApplicationInstanceRecord.GetDefaultInstance(ApplicationName), StringComparison.InvariantCultureIgnoreCase));
                if (defaultInstance != null)
                {
                    instance = defaultInstance;
                }
                else
                {
                    throw new ControlledFailureException(
                        $"There is more than one instance of {ApplicationName} configured on this machine. Please pass --instance=INSTANCENAME when invoking this command to target a specific instance. Available instances: {string.Join(", ", instances.Select(i => i.InstanceName))}.");
                }
            }

            return instance;
        }
    }
}