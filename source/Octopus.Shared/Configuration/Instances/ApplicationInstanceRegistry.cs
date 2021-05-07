using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Octopus.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration.Instances
{
    class ApplicationInstanceRegistry : ApplicationInstanceLocator, IApplicationInstanceRegistry, IApplicationConfigurationStrategy
    {
        readonly IRegistryApplicationInstanceStore registryApplicationInstanceStore;

        public ApplicationInstanceRegistry(
            StartUpInstanceRequest startUpInstanceRequest,
            ISystemLog log,
            IOctopusFileSystem fileSystem,
            IRegistryApplicationInstanceStore registryApplicationInstanceStore) : base(startUpInstanceRequest.ApplicationName, log, fileSystem, registryApplicationInstanceStore)
        {
            this.registryApplicationInstanceStore = registryApplicationInstanceStore;
        }

        public int Priority => 1;

        public IAggregatableKeyValueStore LoadedConfiguration(ApplicationRecord applicationInstance)
        {
            var instance = applicationInstance as ApplicationInstanceRecord;
            if (instance == null)
                throw new ArgumentException("Incorrect application instance record type", nameof(applicationInstance));

            // If the entry is still in the registry then migrate it to the file index
            MigrateInstance(instance);

            return new XmlFileKeyValueStore(FileSystem, instance.ConfigurationFilePath);
        }

        void WriteInstanceConfiguration(Instance instance, string path)
        {
            var data = JsonConvert.SerializeObject(instance, Formatting.Indented);
            try
            {
                FileSystem.OverwriteFile(path, data);
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.Warn(ex.Message);
                throw new ControlledFailureException($"Unable to write file '{path}' as user '{Environment.UserName}'. Please check file permissions.");
            }
        }

        /*
        public void CreateDefaultInstance(string configurationFile, string? homeDirectory = null)
        {
            CreateInstance(ApplicationInstanceRecord.GetDefaultInstance(startUpInstanceRequest.ApplicationName), configurationFile, homeDirectory);
        }
        */

        /*public void CreateInstance(string instanceName, string configurationFile, string? homeDirectory = null)
        {
            var parentDirectory = Path.GetDirectoryName(configurationFile) ?? throw new ArgumentException("Directory required", nameof(configurationFile));
            FileSystem.EnsureDirectoryExists(parentDirectory);

            if (!FileSystem.FileExists(configurationFile))
            {
                Log.Info("Creating empty configuration file: " + configurationFile);
                FileSystem.OverwriteFile(configurationFile, @"<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>");
            }

            var instance = new ApplicationInstanceRecord(instanceName, configurationFile);
            SaveInstance(instance);

            var homeConfig = new WritableHomeConfiguration(startUpInstanceRequest.ApplicationName, new XmlFileKeyValueStore(FileSystem, configurationFile));
            var home = !string.IsNullOrWhiteSpace(homeDirectory) ? homeDirectory : parentDirectory;
            Log.Info($"Setting home directory to: {home}");
            homeConfig.SetHomeDirectory(home);
        }*/

        public void RegisterInstance(ApplicationInstanceRecord instanceRecord)
        {
            var instancesFolder = InstancesFolder();
            if (!FileSystem.DirectoryExists(instancesFolder))
                FileSystem.CreateDirectory(instancesFolder);
            var instanceConfiguration = Path.Combine(instancesFolder, InstanceFileName(instanceRecord.InstanceName) + ".config");
            var instance = TryLoadInstanceConfiguration(instanceConfiguration) ?? new Instance(instanceRecord.InstanceName, instanceRecord.ConfigurationFilePath);

            instance.ConfigurationFilePath = instanceRecord.ConfigurationFilePath;

            WriteInstanceConfiguration(instance, instanceConfiguration);
            Log.Info("Saving instance: " + instance.Name);
        }

        public void DeleteInstance(string instanceName)
        {
            var instancesFolder = InstancesFolder();
            var instanceConfiguration = Path.Combine(instancesFolder, InstanceFileName(instanceName) + ".config");

            try
            {
                FileSystem.DeleteFile(instanceConfiguration);
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.Warn(ex.Message);
                throw new ControlledFailureException($"Unable to delete file '{instanceConfiguration}' as user '{Environment.UserName}'. Please check file permissions.");
            }

            registryApplicationInstanceStore.DeleteFromRegistry(instanceName);

            Log.Info($"Deleted instance: {instanceName}");
        }

        public void MigrateInstance(ApplicationInstanceRecord instanceRecord)
        {
            var instanceName = instanceRecord.InstanceName;
            var instancesFolder = InstancesFolder();
            if (File.Exists(Path.Combine(instancesFolder, InstanceFileName(instanceName) + ".config")))
                return;

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var registryInstance = registryApplicationInstanceStore.GetInstanceFromRegistry(instanceName);
                if (registryInstance != null)
                {
                    Log.Info($"Migrating {ApplicationName} instance from registry - {instanceName}");
                    try
                    {
                        RegisterInstance(instanceRecord);
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
