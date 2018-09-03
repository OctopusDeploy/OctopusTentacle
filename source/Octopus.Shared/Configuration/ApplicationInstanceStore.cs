using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class ApplicationInstanceStore : IApplicationInstanceStore
    {
        private readonly IOctopusFileSystem fileSystem;
        private readonly IRegistryApplicationInstanceStore registryApplicationInstanceStore;

        public ApplicationInstanceStore(IOctopusFileSystem fileSystem, IRegistryApplicationInstanceStore registryApplicationInstanceStore)
        {
            this.fileSystem = fileSystem;
            this.registryApplicationInstanceStore = registryApplicationInstanceStore;
        }

        public class Instance
        {
            public string Name { get; set; }
            public string ConfigurationFilePath { get; set; }
        }

        private static string InstancesFolder(ApplicationName name)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), name.ToString());
        }

        public IList<ApplicationInstanceRecord> ListInstances(ApplicationName name)
        {
            var instancesFolder = InstancesFolder(name);

            if (!fileSystem.DirectoryExists(instancesFolder))
            {
                // migrate the list of instances from the registry to the folder.
                var listFromRegistry = registryApplicationInstanceStore.GetListFromRegistry(name);
                foreach (var instanceRecord in listFromRegistry)
                {
                    SaveInstance(instanceRecord);
                }
                registryApplicationInstanceStore.DeleteFromRegistry(name);
            }

            var results = Directory.EnumerateFiles(instancesFolder)
                .Select(LoadInstanceConfiguration)
                .Select(instance => new ApplicationInstanceRecord(instance.Name, name, instance.ConfigurationFilePath))
                .ToList();

            return results;
        }

        Instance LoadInstanceConfiguration(string path)
        {
            using (var file = fileSystem.OpenFile(path, FileAccess.Read))
            using (var reader = new StreamReader(file, true))
            {
                var data = reader.ReadToEnd();
                var instance = JsonConvert.DeserializeObject<Instance>(data);
                return instance;
            }
        }

        void WriteInstanceConfiguration(Instance instance, string path)
        {
            using (var file = fileSystem.OpenFile(path, FileAccess.Write))
            using (var writer = new StreamWriter(file))
            {
                var data = JsonConvert.SerializeObject(instance);
                writer.Write(data);
            }
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
            var instanceConfiguration = Path.Combine(instancesFolder, InstanceFileName(instanceRecord.InstanceName) + ".config");
            var instance = LoadInstanceConfiguration(instanceConfiguration);
            instance.ConfigurationFilePath = instanceRecord.ConfigurationFilePath;

            WriteInstanceConfiguration(instance, instanceConfiguration);
        }

        public void DeleteInstance(ApplicationInstanceRecord instanceRecord)
        {
            var instancesFolder = InstancesFolder(instanceRecord.ApplicationName);
            var instanceConfiguration = Path.Combine(instancesFolder, InstanceFileName(instanceRecord.InstanceName) + ".config");

            fileSystem.DeleteFile(instanceConfiguration);
        }
    }
}