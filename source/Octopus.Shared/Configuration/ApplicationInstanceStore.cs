using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Octopus.Diagnostics;
using Octopus.Shared.Threading;
using Octopus.Shared.Util;

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

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            using (var mutex = new MachineWideMutex().Acquire($"{name}InstanceStore", "migrating from registry", cts.Token))
            {
                if (!fileSystem.DirectoryExists(instancesFolder))
                {
                    log.InfoFormat("Migrating {0} instance data from the registry", name.ToString());
                    // migrate the list of instances from the registry to the folder.
                    var listFromRegistry = registryApplicationInstanceStore.GetListFromRegistry(name);
                    foreach (var instanceRecord in listFromRegistry)
                    {
                        log.InfoFormat("Migrating instance - {0}", instanceRecord.InstanceName);
                        try
                        {
                            SaveInstance(instanceRecord);
                            registryApplicationInstanceStore.DeleteFromRegistry(name, instanceRecord.InstanceName);
                        }
                        catch (Exception ex)
                        {
                            log.Error(ex, "Error migrating instance data");
                            throw;
                        }
                    }
                }
            }

            var results = fileSystem.EnumerateFiles(instancesFolder)
                .Select(LoadInstanceConfiguration)
                .Select(instance => new ApplicationInstanceRecord(instance.Name, name, instance.ConfigurationFilePath))
                .ToList();

            return results;
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
    }
}