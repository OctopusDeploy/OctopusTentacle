using System;
using System.IO;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class ApplicationInstanceSelector : IApplicationInstanceSelector
    {
        readonly ApplicationName applicationName;
        readonly IOctopusFileSystem fileSystem;
        readonly IApplicationInstanceStore instanceStore;
        readonly ILog log;

        public ApplicationInstanceSelector(ApplicationName applicationName, IOctopusFileSystem fileSystem, IApplicationInstanceStore instanceStore, ILog log)
        {
            this.applicationName = applicationName;
            this.fileSystem = fileSystem;
            this.instanceStore = instanceStore;
            this.log = log;
        }

        public LoadedApplicationInstance Current { get; private set; }
        public event Action Loaded;

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

        public void LoadDefaultInstance()
        {
            var instance = instanceStore.GetDefaultInstance(applicationName);
            if (instance == null)
            {
                throw new ArgumentException("The default instance of " + applicationName + " has not been created. Either pass --instance INSTANCENAME when invoking this command, or run the setup wizard.");
            }

            Load(instance);
        }

        public void LoadInstance(string instanceName)
        {
            var instance = instanceStore.GetInstance(applicationName, instanceName);
            if (instance == null)
            {
                throw new ArgumentException("Instance " + instanceName + " of application " + applicationName + " has not been created. Check the instance name or run the setup wizard.");
            }

            Load(instance);
        }

        public void CreateDefaultInstance(string configurationFile)
        {
            CreateInstance(ApplicationInstanceRecord.GetDefaultInstance(applicationName), configurationFile);
        }

        public void CreateInstance(string instanceName, string configurationFile)
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
        }

        void Load(ApplicationInstanceRecord record)
        {
            if (record == null) throw new ArgumentNullException("record");
            Current = new LoadedApplicationInstance(applicationName, record.InstanceName, record.ConfigurationFilePath, new XmlFileKeyValueStore(record.ConfigurationFilePath));
            OnLoaded();
        }

        protected virtual void OnLoaded()
        {
            var handler = Loaded;
            if (handler != null) handler();
        }
    }
}