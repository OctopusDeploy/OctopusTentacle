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
        string instanceName;
        readonly IOctopusFileSystem fileSystem;
        readonly IApplicationInstanceStore instanceStore;
        readonly ILog log;
        readonly object @lock = new object();

        public ApplicationInstanceSelector(ApplicationName applicationName,
            string instanceName,
            IOctopusFileSystem fileSystem,
            IApplicationInstanceStore instanceStore,
            ILog log)
        {
            this.applicationName = applicationName;
            this.instanceName = instanceName;
            this.fileSystem = fileSystem;
            this.instanceStore = instanceStore;
            this.log = log;
        }

        LoadedApplicationInstance current;

        public bool TryLoadCurrentInstance(out LoadedApplicationInstance instance)
        {
            instance = null;
            try
            {
                instance = Current;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public LoadedApplicationInstance Current
        {
            get
            {
                if (current == null)
                {
                    lock (@lock)
                    {
                        if (current == null)
                            current = DoLoad();
                    }
                }
                return current;
            }
        }

        LoadedApplicationInstance DoLoad()
        {
            LoadedApplicationInstance instance;
            if (string.IsNullOrWhiteSpace(instanceName))
                instance = LoadDefaultInstance();
            else
                instance = LoadInstance(instanceName);

            // BEWARE if you try to resolve HomeConfiguration from the container you'll create a loop
            // back to here
            var homeConfig = new HomeConfiguration(applicationName, instance.Configuration);
            var logInit = new LogInitializer(new LoggingConfiguration(homeConfig), fileSystem);
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
                    ? $"There is no default instance of {applicationName} configured on this machine. Please pass --instance=INSTANCENAME when invoking this command to target a specific instance. Available instances: {string.Join(", ", instances.Select(i => i.InstanceName))}"
                    : $"There are no instances of {applicationName} configured on this machine. Please run the setup wizard or configure an instance using the command-line interface.");
            }

            return Load(instance);
        }

        LoadedApplicationInstance LoadInstance(string instanceName)
        {
            var instance = instanceStore.GetInstance(applicationName, instanceName);
            if (instance == null)
            {
                var instances = instanceStore.ListInstances(applicationName);
                throw new ControlledFailureException(instances.Any()
                    ? $"Instance {instanceName} of {applicationName} has not been configured on this machine. Available instances: {string.Join(", ", instances.Select(i => i.InstanceName))}"
                    : $"There are no instances of {applicationName} configured on this machine. Please run the setup wizard or configure an instance using the command-line interface.");
            }

            return Load(instance);
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

            var homeConfig = new HomeConfiguration(applicationName, new XmlFileKeyValueStore(configurationFile));
            var home = !string.IsNullOrWhiteSpace(homeDirectory) ? homeDirectory : parentDirectory;
            log.Info($"Setting home directory to: {home}");
            homeConfig.HomeDirectory = home;

            this.instanceName = instanceName;
            DoLoad();
        }

        LoadedApplicationInstance Load(ApplicationInstanceRecord record)
        {
            if (record == null) throw new ArgumentNullException("record");
            return new LoadedApplicationInstance(
                applicationName,
                record.InstanceName,
                record.ConfigurationFilePath,
                new XmlFileKeyValueStore(record.ConfigurationFilePath));
        }
    }
}