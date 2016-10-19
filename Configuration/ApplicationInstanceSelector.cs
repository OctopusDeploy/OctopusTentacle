using System;
using System.IO;
using Octopus.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class ApplicationInstanceSelector : IApplicationInstanceSelector
    {
        readonly ApplicationName applicationName;
        readonly string instanceName;
        readonly IOctopusFileSystem fileSystem;
        readonly IApplicationInstanceStore instanceStore;
        readonly ILog log;
        readonly Lazy<ILogInitializer> logInitializer;

        public ApplicationInstanceSelector(ApplicationName applicationName,
            string instanceName,
            IOctopusFileSystem fileSystem,
            IApplicationInstanceStore instanceStore,
            ILog log,
            Lazy<ILogInitializer> logInitializer)
        {
            this.applicationName = applicationName;
            this.instanceName = instanceName;
            this.fileSystem = fileSystem;
            this.instanceStore = instanceStore;
            this.log = log;
            this.logInitializer = logInitializer;
        }
        
        private LoadedApplicationInstance current;
        public LoadedApplicationInstance Current
        {
            get
            {
                if (current == null)
                {
                    current = DoLoad();
                    logInitializer.Value.Start();
                }
                return current;
            }
        }

        LoadedApplicationInstance DoLoad()
        {
            if (string.IsNullOrWhiteSpace(instanceName))
                return LoadDefaultInstance();
            else
                return LoadInstance(instanceName);
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

        private LoadedApplicationInstance LoadDefaultInstance()
        {
            var instance = instanceStore.GetDefaultInstance(applicationName);
            if (instance == null)
                throw new ArgumentException("The default instance of " + applicationName + " has not been created. Either pass --instance INSTANCENAME when invoking this command, or run the setup wizard.");

            return Load(instance);
        }

        private LoadedApplicationInstance LoadInstance(string instanceName)
        {
            var instance = instanceStore.GetInstance(applicationName, instanceName);
            if (instance == null)
                throw new ArgumentException("Instance " + instanceName + " of application " + applicationName + " has not been created. Check the instance name or run the setup wizard.");

            return Load(instance);
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

        LoadedApplicationInstance Load(ApplicationInstanceRecord record)
        {
            if (record == null) throw new ArgumentNullException("record");
            return new LoadedApplicationInstance(applicationName, record.InstanceName, record.ConfigurationFilePath, new XmlFileKeyValueStore(record.ConfigurationFilePath));
        }
    }
}