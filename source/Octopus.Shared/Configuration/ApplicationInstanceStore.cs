using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Octopus.Diagnostics;
using Octopus.Shared.Threading;
using Octopus.Shared.Util;
using Microsoft.Win32;

namespace Octopus.Shared.Configuration
{
    public class ApplicationInstanceStore : IApplicationInstanceStore
    {
        private readonly ILog log;
        private readonly IOctopusFileSystem fileSystem;
        private readonly IRegistryApplicationInstanceStore registryApplicationInstanceStore;
        private readonly string machineConfigurationHomeDirectory;

        public ApplicationInstanceStore(
            ILog log,
            IOctopusFileSystem fileSystem, 
            IRegistryApplicationInstanceStore registryApplicationInstanceStore)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.registryApplicationInstanceStore = registryApplicationInstanceStore;

            machineConfigurationHomeDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Octopus");
        }

        public class Instance
        {
            public string Name { get; set; }
            public string ConfigurationFilePath { get; set; }
        }

        private string InstancesFolder(ApplicationName name)
        {
            return Path.Combine(machineConfigurationHomeDirectory, name.ToString(), "Instances");
        }

        public IList<ApplicationInstanceRecord> ListInstances(ApplicationName name)
        {
            var instancesFolder = InstancesFolder(name);
            
            var listFromRegistry = registryApplicationInstanceStore.GetListFromRegistry(name);
            var listFromFileSystem = Enumerable.Empty<ApplicationInstanceRecord>();
            if (fileSystem.DirectoryExists(instancesFolder))
            {
                listFromFileSystem = fileSystem.EnumerateFiles(instancesFolder)
                    .Select(LoadInstanceConfiguration)
                    .Select(instance => new ApplicationInstanceRecord(instance.Name, name, instance.ConfigurationFilePath))
                    .ToList();
            }

            var combinedInstanceList = listFromFileSystem.Union(listFromRegistry).OrderBy(i => i.InstanceName);
            return combinedInstanceList.ToList();
        }

        Instance LoadInstanceConfiguration(string path)
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

        public ApplicationInstanceRecord GetInstance(ApplicationName name, string instanceName)
        {
            return ListInstances(name).SingleOrDefault(s => s.InstanceName == instanceName);
        }

        public ApplicationInstanceRecord GetDefaultInstance(ApplicationName name)
        {
            return GetInstance(name, ApplicationInstanceRecord.GetDefaultInstance(name));
        }

        string InstanceFileName(string instanceName)
        {
            return instanceName.Replace(' ', '-').ToLower();
        }

        public void SaveInstance(ApplicationInstanceRecord instanceRecord)
        {
            var instancesFolder = InstancesFolder(instanceRecord.ApplicationName);
            if (!fileSystem.DirectoryExists(instancesFolder))
            {
                fileSystem.CreateDirectory(instancesFolder);
            }
            var instanceConfiguration = Path.Combine(instancesFolder, InstanceFileName(instanceRecord.InstanceName) + ".config");
            var instance = LoadInstanceConfiguration(instanceConfiguration) ?? new Instance
            {
                Name = instanceRecord.InstanceName
            };

            instance.ConfigurationFilePath = instanceRecord.ConfigurationFilePath;

            WriteInstanceConfiguration(instance, instanceConfiguration);
        }

        public void DeleteInstance(ApplicationInstanceRecord instanceRecord)
        {
            var instancesFolder = InstancesFolder(instanceRecord.ApplicationName);
            var instanceConfiguration = Path.Combine(instancesFolder, InstanceFileName(instanceRecord.InstanceName) + ".config");

            fileSystem.DeleteFile(instanceConfiguration);
        }

        public void MigrateInstance(ApplicationInstanceRecord instanceRecord)
        {
            var applicationName = instanceRecord.ApplicationName;
            var instanceName = instanceRecord.InstanceName;
            var instancesFolder = InstancesFolder(instanceRecord.ApplicationName);
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var registryInstance = registryApplicationInstanceStore.GetInstanceFromRegistry(applicationName, instanceName);
                if (registryInstance != null)
                {
                    bool fileInstanceSuccessful = false;
                    log.Info($"Migrating {applicationName} instance from registry - {instanceName}");
                    try
                    {
                        SaveInstance(instanceRecord);
                        fileInstanceSuccessful = true;
                        registryApplicationInstanceStore.DeleteFromRegistry(applicationName, instanceName);
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex, "Error migrating instance data");
                        if (fileInstanceSuccessful)
                        {
                            log.Error($"Rolling back {applicationName} instance from filesystem due to error - {instanceName}");
                            DeleteInstance(instanceRecord);
                        }
                        throw;
                    }
                }
            }
        }
    }
}