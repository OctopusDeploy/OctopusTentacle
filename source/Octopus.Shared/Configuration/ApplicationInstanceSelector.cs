using System;
using System.IO;
using System.Linq;
using Octopus.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class ApplicationInstanceSelector : IApplicationInstanceSelector
    {
        readonly ApplicationName applicationName;
        string currentInstanceName;
        readonly IOctopusFileSystem fileSystem;
        readonly IApplicationInstanceStore instanceStore;
        readonly ILog log;
        readonly object @lock = new object();

        public ApplicationInstanceSelector(ApplicationName applicationName,
            string currentInstanceName,
            IOctopusFileSystem fileSystem,
            IApplicationInstanceStore instanceStore,
            ILog log)
        {
            this.applicationName = applicationName;
            this.currentInstanceName = currentInstanceName;
            this.fileSystem = fileSystem;
            this.instanceStore = instanceStore;
            this.log = log;
        }

        LoadedApplicationInstance current;

        public bool TryGetCurrentInstance(out LoadedApplicationInstance instance)
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
            var instance = string.IsNullOrWhiteSpace(currentInstanceName) ? LoadDefaultInstance() : LoadInstance(currentInstanceName);

            // BEWARE if you try to resolve HomeConfiguration from the container you'll create a loop
            // back to here
            var homeConfig = new HomeConfiguration(applicationName, instance.Configuration);
            var logInit = new LogInitializer(new LoggingConfiguration(homeConfig));
            logInit.Start();

            return instance;
        }

        public void DeleteDefaultInstance()
        {
            var instance = instanceStore.GetDefaultInstance(applicationName);
            if (instance == null) return;
            instanceStore.DeleteInstance(instance);
            log.Info("Deleted default instance");
        }

        public void DeleteInstance(string instanceName)
        {
            var instance = instanceStore.GetInstance(applicationName, instanceName);
            if (instance == null) return;
            instanceStore.DeleteInstance(instance);
            log.Info("Deleted instance: " + instanceName);
        }

        LoadedApplicationInstance LoadDefaultInstance()
        {
            var instance = instanceStore.GetDefaultInstance(applicationName);
            if (instance == null)
            {
                var instances = instanceStore.ListInstances(applicationName);
                throw new ControlledFailureException(instances.Any()
                    ? $"There is no default instance of {applicationName} configured on this machine. Please pass --instance=INSTANCENAME when invoking this command to target a specific instance. Available instances: {string.Join(", ", instances.Select(i => i.InstanceName))}."
                    : BuildNoInstancesMessage(applicationName));
            }

            return LoadFrom(instance);
        }

        LoadedApplicationInstance LoadInstance(string instanceName)
        {
            var instance = instanceStore.GetInstance(applicationName, instanceName);
            if (instance == null)
            {
                var instances = instanceStore.ListInstances(applicationName);
                throw new ControlledFailureException(instances.Any()
                    ? $"Instance {instanceName} of {applicationName} has not been configured on this machine. Available instances: {string.Join(", ", instances.Select(i => i.InstanceName))}."
                    : BuildNoInstancesMessage(applicationName));
            }

            return LoadFrom(instance);
        }

        static string BuildNoInstancesMessage(ApplicationName applicationName)
        {
            return $"There are no instances of {applicationName} configured on this machine. Please run the setup wizard or configure an instance using the command-line interface.";
        }

        public void CreateDefaultInstance(string configurationFile, string homeDirectory = null)
        {
            CreateInstance(ApplicationInstanceRecord.GetDefaultInstance(applicationName), configurationFile, homeDirectory);
        }

        public void CreateInstance(string instanceName, string configurationFile, string homeDirectory = null)
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
    }
}