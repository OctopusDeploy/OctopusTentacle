using System;
using System.IO;
using System.Linq;
using Octopus.Diagnostics;
using Octopus.Shared.Startup;
using Octopus.Shared.Util;
using System.Diagnostics.CodeAnalysis;

namespace Octopus.Shared.Configuration
{
    public class ApplicationInstanceSelector : IApplicationInstanceSelector
    {
        readonly ApplicationName applicationName;
        string currentInstanceName;
        readonly IOctopusFileSystem fileSystem;
        readonly IApplicationInstanceStore instanceStore;
        readonly ILog log;
        private readonly ILogFileOnlyLogger logFileOnlyLogger;
        readonly object @lock = new object();

        public ApplicationInstanceSelector(ApplicationName applicationName,
            string currentInstanceName,
            IOctopusFileSystem fileSystem,
            IApplicationInstanceStore instanceStore,
            ILog log,
            ILogFileOnlyLogger logFileOnlyLogger)
        {
            this.applicationName = applicationName;
            this.currentInstanceName = currentInstanceName;
            this.fileSystem = fileSystem;
            this.instanceStore = instanceStore;
            this.log = log;
            this.logFileOnlyLogger = logFileOnlyLogger;
        }

        LoadedApplicationInstance? current;

        public bool TryGetCurrentInstance([NotNullWhen(true)] out LoadedApplicationInstance? instance)
        {
            instance = null;
            try
            {
                instance = GetCurrentInstance();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public LoadedApplicationInstance GetCurrentInstance()
        {
            if (current == null)
            {
                lock (@lock)
                {
                    if (current == null)
                        current = LoadCurrentInstance();
                }
            }
            return current;
        }

        LoadedApplicationInstance LoadCurrentInstance()
        {
            var instance = LoadFrom(LoadInstance());

            // BEWARE if you try to resolve HomeConfiguration from the container you'll create a loop
            // back to here
            var homeConfig = new HomeConfiguration(applicationName, instance.Configuration);
            var logInit = new LogInitializer(new LoggingConfiguration(homeConfig), logFileOnlyLogger);
            logInit.Start();

            return instance;
        }

        public void DeleteInstance()
        {
            var instance = LoadInstance();
            if (instance == null) return;
            instanceStore.DeleteInstance(instance);
            log.Info($"Deleted instance: {instance.InstanceName}");
        }

        internal ApplicationInstanceRecord LoadInstance()
        {
            ApplicationInstanceRecord? instance = null;
            var anyInstances = instanceStore.AnyInstancesConfigured(applicationName);
            if (!anyInstances)
                throw new ControlledFailureException(
                    $"There are no instances of {applicationName} configured on this machine. Please run the setup wizard or configure an instance using the command-line interface.");

            instance = string.IsNullOrEmpty(currentInstanceName) ? TryLoadDefaultInstance() : TryLoadInstanceByName();

            if (instance == null)
            {
                var instances = instanceStore.ListInstances(applicationName);
                throw new ControlledFailureException(
                    $"Instance {currentInstanceName} of {applicationName} has not been configured on this machine. Available instances: {string.Join(", ", instances.Select(i => i.InstanceName))}.");
            }

            instanceStore.MigrateInstance(instance);
            return instance;
        }


        public void CreateDefaultInstance(string configurationFile, string? homeDirectory = null)
        {
            CreateInstance(ApplicationInstanceRecord.GetDefaultInstance(applicationName), configurationFile, homeDirectory);
        }

        public void CreateInstance(string instanceName, string configurationFile, string? homeDirectory = null)
        {
            var parentDirectory = Path.GetDirectoryName(configurationFile);
            fileSystem.EnsureDirectoryExists(parentDirectory);

            if (!fileSystem.FileExists(configurationFile))
            {
                log.Info("Creating empty configuration file: " + configurationFile);
                fileSystem.OverwriteFile(configurationFile, @"<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>");
            }

            log.Info("Saving instance: " + instanceName);
            var instance = new ApplicationInstanceRecord(instanceName, applicationName, configurationFile);
            instanceStore.SaveInstance(instance);

            var homeConfig = new HomeConfiguration(applicationName, new XmlFileKeyValueStore(fileSystem, configurationFile));
            var home = !string.IsNullOrWhiteSpace(homeDirectory) ? homeDirectory : parentDirectory;
            log.Info($"Setting home directory to: {home}");
            homeConfig.HomeDirectory = home;

            currentInstanceName = instanceName;
            LoadCurrentInstance();
        }

        LoadedApplicationInstance LoadFrom(ApplicationInstanceRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            return new LoadedApplicationInstance(
                applicationName,
                record.InstanceName,
                record.ConfigurationFilePath,
                new XmlFileKeyValueStore(fileSystem, record.ConfigurationFilePath));
        }

        private ApplicationInstanceRecord? TryLoadInstanceByName()
        {
            var instance = instanceStore.GetInstance(applicationName, currentInstanceName);
            if (instance == null)
            {
                var instances = instanceStore.ListInstances(applicationName);
                var caseInsensitiveMatches = instances.Where(s => string.Equals(s.InstanceName, currentInstanceName, StringComparison.InvariantCultureIgnoreCase)).ToArray();
                if (caseInsensitiveMatches.Length > 1)
                    throw new ControlledFailureException(
                        $"Instance {currentInstanceName} of {applicationName} could not be matched to one of the existing instances: {string.Join(", ", instances.Select(i => i.InstanceName))}.");
                if (caseInsensitiveMatches.Length == 1)
                {
                    instance = caseInsensitiveMatches.First();
                }
            }

            return instance;
        }

        private ApplicationInstanceRecord TryLoadDefaultInstance()
        {
            ApplicationInstanceRecord instance;
            var instances = instanceStore.ListInstances(applicationName);
            if (instances.Count == 1)
            {
                instance = instances.First();
            }
            else
            {
                var defaultInstance = instances.FirstOrDefault(s => string.Equals(s.InstanceName, ApplicationInstanceRecord.GetDefaultInstance(applicationName), StringComparison.InvariantCultureIgnoreCase));
                if (defaultInstance != null)
                {
                    instance = defaultInstance;
                }
                else
                {
                    throw new ControlledFailureException(
                        $"There is more than one instance of {applicationName} configured on this machine. Please pass --instance=INSTANCENAME when invoking this command to target a specific instance. Available instances: {string.Join(", ", instances.Select(i => i.InstanceName))}.");
                }
            }

            return instance;
        }
    }
}