using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Octopus.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration.Instances
{
    public class PersistedApplicationInstanceStore : IPersistedApplicationInstanceStore, IApplicationInstanceStrategy
    {
        readonly StartUpInstanceRequest startUpInstanceRequest;
        readonly ILog log;
        readonly IOctopusFileSystem fileSystem;
        readonly IRegistryApplicationInstanceStore registryApplicationInstanceStore;
        readonly string machineConfigurationHomeDirectory;

        public PersistedApplicationInstanceStore(
            StartUpInstanceRequest startUpInstanceRequest,
            ILog log,
            IOctopusFileSystem fileSystem,
            IRegistryApplicationInstanceStore registryApplicationInstanceStore)
        {
            this.startUpInstanceRequest = startUpInstanceRequest;
            this.log = log;
            this.fileSystem = fileSystem;
            this.registryApplicationInstanceStore = registryApplicationInstanceStore;
            machineConfigurationHomeDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Octopus");

            if (!PlatformDetection.IsRunningOnWindows)
            {
                machineConfigurationHomeDirectory = "/etc/octopus";
            }
        }

        public int Priority => 1000;

        public LoadedApplicationInstance LoadedApplicationInstance(ApplicationInstanceRecord applicationInstance)
        {
            var instance = applicationInstance as PersistedApplicationInstanceRecord;
            if (instance == null)
                throw new ArgumentException("Incorrect application instance record type", nameof(applicationInstance));
            
            // If the entry is still in the registry then migrate it to the file index
            MigrateInstance(instance);
            
            return new LoadedPersistedApplicationInstance(applicationInstance.InstanceName, new XmlFileKeyValueStore(fileSystem, instance.ConfigurationFilePath), instance.ConfigurationFilePath);
        }

        public class Instance
        {
            public Instance(string name, string configurationFilePath)
            {
                Name = name;
                ConfigurationFilePath = configurationFilePath;
            }

            public string Name { get; }
            public string ConfigurationFilePath { get; set; }
        }

        internal string InstancesFolder()
        {
            return Path.Combine(machineConfigurationHomeDirectory, startUpInstanceRequest.ApplicationName.ToString(), "Instances");
        }

        public bool AnyInstancesConfigured()
        {
            var instancesFolder = InstancesFolder();
            if (fileSystem.DirectoryExists(instancesFolder))
            {
                if (fileSystem.EnumerateFiles(instancesFolder).Any())
                    return true;
            }
            var listFromRegistry = registryApplicationInstanceStore.GetListFromRegistry();
            return listFromRegistry.Any();
        }

        public IList<ApplicationInstanceRecord> ListInstances()
        {
            var instancesFolder = InstancesFolder();

            var listFromRegistry = registryApplicationInstanceStore.GetListFromRegistry();
            var listFromFileSystem = new List<ApplicationInstanceRecord>();
            if (fileSystem.DirectoryExists(instancesFolder))
            {
                listFromFileSystem = fileSystem.EnumerateFiles(instancesFolder)
                    .Select(LoadInstanceConfiguration)
                    .Select(instance => new PersistedApplicationInstanceRecord(instance.Name, instance.ConfigurationFilePath, instance.Name == PersistedApplicationInstanceRecord.GetDefaultInstance(startUpInstanceRequest.ApplicationName)))
                    .Cast<ApplicationInstanceRecord>()
                    .ToList();
            }

            // for customers running multiple instances on a machine, they may have a version that only understood
            // using the registry. We need to list those too.
            var combinedInstanceList = listFromFileSystem
                .Concat(listFromRegistry.Where(x => listFromFileSystem.All(y => y.InstanceName != x.InstanceName)))
                .OrderBy(i => i.InstanceName);
            return combinedInstanceList.ToList();
        }

        Instance LoadInstanceConfiguration(string path)
        {
            var result = TryLoadInstanceConfiguration(path);
            if (result == null)
                throw new ArgumentException($"Could not load instance at path {path}");
            return result;
        }

        Instance? TryLoadInstanceConfiguration(string path)
        {
            if (!fileSystem.FileExists(path))
                return null;

            var data = fileSystem.ReadFile(path);
            var instance = JsonConvert.DeserializeObject<Instance>(data);
            return instance;
        }

        void WriteInstanceConfiguration(Instance instance, string path)
        {
            var data = JsonConvert.SerializeObject(instance, Formatting.Indented);
            fileSystem.OverwriteFile(path, data);
        }

        public PersistedApplicationInstanceRecord? GetInstance(string? instanceName)
        {
            var instancesFolder = InstancesFolder();
            if (instanceName == null)
                instanceName = PersistedApplicationInstanceRecord.GetDefaultInstance(startUpInstanceRequest.ApplicationName);

            if (fileSystem.DirectoryExists(instancesFolder))
            {
                var instanceConfiguration = Path.Combine(instancesFolder, InstanceFileName(instanceName) + ".config");
                var instance = TryLoadInstanceConfiguration(instanceConfiguration);
                if (instance != null)
                {
                    return new PersistedApplicationInstanceRecord(instance.Name, instance.ConfigurationFilePath, instanceName == PersistedApplicationInstanceRecord.GetDefaultInstance(startUpInstanceRequest.ApplicationName));
                }
            }

            // for customers running multiple instances on a machine, they may have a version that only understood
            // using the registry. We need to fall back to there if it doesn't exist in the folder yet.
            var listFromRegistry = registryApplicationInstanceStore.GetListFromRegistry();
            return listFromRegistry.FirstOrDefault(x => x.InstanceName == instanceName);
        }

        string InstanceFileName(string instanceName)
        {
            return instanceName.Replace(' ', '-').ToLower();
        }

        public void CreateDefaultInstance(string configurationFile, string? homeDirectory = null)
        {
            CreateInstance(PersistedApplicationInstanceRecord.GetDefaultInstance(startUpInstanceRequest.ApplicationName), configurationFile, homeDirectory);
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
            var instance = new PersistedApplicationInstanceRecord(instanceName, configurationFile, instanceName == PersistedApplicationInstanceRecord.GetDefaultInstance(startUpInstanceRequest.ApplicationName));
            SaveInstance(instance);

            var homeConfig = new HomeConfiguration(startUpInstanceRequest.ApplicationName, new XmlFileKeyValueStore(fileSystem, configurationFile));
            var home = !string.IsNullOrWhiteSpace(homeDirectory) ? homeDirectory : parentDirectory;
            log.Info($"Setting home directory to: {home}");
            homeConfig.HomeDirectory = home;
        }

        public void SaveInstance(PersistedApplicationInstanceRecord instanceRecord)
        {
            var instancesFolder = InstancesFolder();
            if (!fileSystem.DirectoryExists(instancesFolder))
            {
                fileSystem.CreateDirectory(instancesFolder);
            }
            var instanceConfiguration = Path.Combine(instancesFolder, InstanceFileName(instanceRecord.InstanceName) + ".config");
            var instance = TryLoadInstanceConfiguration(instanceConfiguration) ?? new Instance(instanceRecord.InstanceName, instanceRecord.ConfigurationFilePath);

            instance.ConfigurationFilePath = instanceRecord.ConfigurationFilePath;

            WriteInstanceConfiguration(instance, instanceConfiguration);
        }

        public void DeleteInstance(PersistedApplicationInstanceRecord instanceRecord)
        {
            var instancesFolder = InstancesFolder();
            var instanceConfiguration = Path.Combine(instancesFolder, InstanceFileName(instanceRecord.InstanceName) + ".config");

            fileSystem.DeleteFile(instanceConfiguration);
        }

        public void MigrateInstance(PersistedApplicationInstanceRecord instanceRecord)
        {
            var instanceName = instanceRecord.InstanceName;
            var instancesFolder = InstancesFolder();
            if (File.Exists(Path.Combine(instancesFolder, InstanceFileName(instanceName) + ".config")))
            {
                return;
            }

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var registryInstance = registryApplicationInstanceStore.GetInstanceFromRegistry(instanceName);
                if (registryInstance != null )
                {
                    log.Info($"Migrating {startUpInstanceRequest.ApplicationName} instance from registry - {instanceName}");
                    try
                    {
                        SaveInstance(instanceRecord);
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex, "Error migrating instance data");
                        throw;
                    }
                }
            }
        }
    }
}