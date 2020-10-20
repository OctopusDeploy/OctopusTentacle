using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Octopus.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration.Instances
{
    class ApplicationInstanceStore : ApplicationInstanceLocator, IApplicationInstanceStore
    {
        public ApplicationInstanceStore(ApplicationName applicationName,
            ILog log,
            IOctopusFileSystem fileSystem,
            IRegistryApplicationInstanceStore registryApplicationInstanceStore) : base(applicationName, log, fileSystem, registryApplicationInstanceStore)
        {
        }

        void WriteInstanceConfiguration(Instance instance, string path)
        {
            var data = JsonConvert.SerializeObject(instance, Formatting.Indented);
            FileSystem.OverwriteFile(path, data);
        }

        public void SaveInstance(ApplicationInstanceRecord instanceRecord)
        {
            var instancesFolder = InstancesFolder();
            if (!FileSystem.DirectoryExists(instancesFolder))
            {
                FileSystem.CreateDirectory(instancesFolder);
            }
            var instanceConfiguration = Path.Combine(instancesFolder, InstanceFileName(instanceRecord.InstanceName) + ".config");
            var instance = TryLoadInstanceConfiguration(instanceConfiguration) ?? new Instance(instanceRecord.InstanceName, instanceRecord.ConfigurationFilePath);

            instance.ConfigurationFilePath = instanceRecord.ConfigurationFilePath;

            WriteInstanceConfiguration(instance, instanceConfiguration);
        }

        public void DeleteInstance(string instanceName)
        {
            var instancesFolder = InstancesFolder();
            var instanceConfiguration = Path.Combine(instancesFolder, InstanceFileName(instanceName) + ".config");

            FileSystem.DeleteFile(instanceConfiguration);
            Log.Info($"Deleted instance: {instanceName}");
        }

        public void MigrateInstance(ApplicationInstanceRecord instanceRecord)
        {
            var instanceName = instanceRecord.InstanceName;
            var instancesFolder = InstancesFolder();
            if (File.Exists(Path.Combine(instancesFolder, InstanceFileName(instanceName) + ".config")))
            {
                return;
            }

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var registryInstance = RegistryApplicationInstanceStore.GetInstanceFromRegistry(instanceName);
                if (registryInstance != null )
                {
                    Log.Info($"Migrating {ApplicationName} instance from registry - {instanceName}");
                    try
                    {
                        SaveInstance(instanceRecord);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error migrating instance data");
                        throw;
                    }
                }
            }
        }
    }
}