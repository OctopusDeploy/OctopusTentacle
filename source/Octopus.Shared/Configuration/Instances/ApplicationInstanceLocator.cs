using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Octopus.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration.Instances
{
    class ApplicationInstanceLocator : IApplicationInstanceLocator
    {
        readonly string machineConfigurationHomeDirectory;

        public ApplicationInstanceLocator(ApplicationName applicationName,
            ILog log,
            IOctopusFileSystem fileSystem,
            IRegistryApplicationInstanceStore registryApplicationInstanceStore)
        {
            ApplicationName = applicationName;
            Log = log;
            FileSystem = fileSystem;
            RegistryApplicationInstanceStore = registryApplicationInstanceStore;

            machineConfigurationHomeDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Octopus");

            if (!PlatformDetection.IsRunningOnWindows)
            {
                machineConfigurationHomeDirectory = "/etc/octopus";
            }
        }

        protected ApplicationName ApplicationName { get; }
        protected ILog Log { get; }
        protected IOctopusFileSystem FileSystem { get; }
        protected IRegistryApplicationInstanceStore RegistryApplicationInstanceStore { get; }

        internal string InstancesFolder()
        {
            return Path.Combine(machineConfigurationHomeDirectory, ApplicationName.ToString(), "Instances");
        }

        protected string InstanceFileName(string instanceName)
        {
            return instanceName.Replace(' ', '-').ToLower();
        }


        public bool AnyInstancesConfigured()
        {
            var instancesFolder = InstancesFolder();
            if (FileSystem.DirectoryExists(instancesFolder))
            {
                if (FileSystem.EnumerateFiles(instancesFolder).Any())
                    return true;
            }
            var listFromRegistry = RegistryApplicationInstanceStore.GetListFromRegistry();
            return listFromRegistry.Any();
        }

        public PersistedApplicationInstanceRecord? GetInstance(string instanceName)
        {
            var instancesFolder = InstancesFolder();
            if (FileSystem.DirectoryExists(instancesFolder))
            {
                var instanceConfiguration = Path.Combine(instancesFolder, InstanceFileName(instanceName) + ".config");
                var instance = TryLoadInstanceConfiguration(instanceConfiguration);
                if (instance != null)
                {
                    return new PersistedApplicationInstanceRecord(instance.Name, instance.ConfigurationFilePath, PersistedApplicationInstanceRecord.GetDefaultInstance(ApplicationName) == instance.Name);
                }
            }

            // for customers running multiple instances on a machine, they may have a version that only understood
            // using the registry. We need to fall back to there if it doesn't exist in the folder yet.
            var listFromRegistry = RegistryApplicationInstanceStore.GetListFromRegistry();
            return listFromRegistry.FirstOrDefault(x => x.InstanceName == instanceName);
        }

        public IList<PersistedApplicationInstanceRecord> ListInstances()
        {
            var instancesFolder = InstancesFolder();

            var listFromRegistry = RegistryApplicationInstanceStore.GetListFromRegistry();
            var listFromFileSystem = new List<PersistedApplicationInstanceRecord>();
            if (FileSystem.DirectoryExists(instancesFolder))
            {
                listFromFileSystem = FileSystem.EnumerateFiles(instancesFolder)
                    .Select(LoadInstanceConfiguration)
                    .Select(instance => new PersistedApplicationInstanceRecord(instance.Name, instance.ConfigurationFilePath, PersistedApplicationInstanceRecord.GetDefaultInstance(ApplicationName) == instance.Name))
                    .ToList();
            }

            // for customers running multiple instances on a machine, they may have a version that only understood
            // using the registry. We need to list those too.
            var combinedInstanceList = listFromFileSystem
                .Concat(listFromRegistry.Where(x => listFromFileSystem.All(y => y.InstanceName != x.InstanceName)))
                .OrderBy(i => i.InstanceName);
            return combinedInstanceList.ToList();
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

        Instance LoadInstanceConfiguration(string path)
        {
            var result = TryLoadInstanceConfiguration(path);
            if (result == null)
                throw new ArgumentException($"Could not load instance at path {path}");
            return result;
        }

        protected Instance? TryLoadInstanceConfiguration(string path)
        {
            if (!FileSystem.FileExists(path))
                return null;

            var data = FileSystem.ReadFile(path);
            var instance = JsonConvert.DeserializeObject<Instance>(data);
            return instance;
        }
    }
}