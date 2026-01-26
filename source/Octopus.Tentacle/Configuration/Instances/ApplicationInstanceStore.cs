using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Util;
using Octopus.Tentacle.Variables;

namespace Octopus.Tentacle.Configuration.Instances
{
    class ApplicationInstanceStore : IApplicationInstanceStore
    {
        readonly IOctopusFileSystem fileSystem;
        readonly ApplicationName applicationName;
        readonly ISystemLog log;
        readonly IRegistryApplicationInstanceStore registryApplicationInstanceStore;
        readonly string machineConfigurationHomeDirectory;

        public ApplicationInstanceStore(
            ApplicationName applicationName,
            ISystemLog log,
            IOctopusFileSystem fileSystem,
            IRegistryApplicationInstanceStore registryApplicationInstanceStore)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.applicationName = applicationName;
            this.registryApplicationInstanceStore = registryApplicationInstanceStore;
            
            var customMachineConfigurationHomeDirectory = Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleMachineConfigurationHomeDirectory);

            if(!string.IsNullOrWhiteSpace(customMachineConfigurationHomeDirectory))
            {
                machineConfigurationHomeDirectory = customMachineConfigurationHomeDirectory;
            }
            else
            {
                machineConfigurationHomeDirectory = PlatformDetection.IsRunningOnWindows ? 
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Octopus") :
                "/etc/octopus";
            }

            log.Verbose("Machine configuration home directory is " + machineConfigurationHomeDirectory);
        }

        public ApplicationInstanceRecord LoadInstanceDetails(string? instanceName)
        {
            ApplicationInstanceRecord? persistedRecord;
            if (instanceName != null && !string.IsNullOrEmpty(instanceName))
            {
                persistedRecord = GetNamedRegistryRecord(instanceName);
                if (persistedRecord == null)
                    throw new ControlledFailureException($"Instance {instanceName} of {applicationName} has not been configured on this machine. Please pass --instance=INSTANCENAME when invoking this command to target a specific instance. Available instances: {AvailableInstancesText()}.");
            }
            else
            {
                // No `--instance` provided, try get Default
                persistedRecord = GetDefaultRegistryRecord();
                if (persistedRecord == null)
                    throw new ControlledFailureException($"Unable to find default instance of {applicationName}. Available instances: {AvailableInstancesText()}.");
            }

            MigrateInstance(persistedRecord);
            return persistedRecord;
        }

        public void RegisterInstance(ApplicationInstanceRecord instanceRecord)
        {
            if (!fileSystem.DirectoryExists(InstancesFolder))
                fileSystem.CreateDirectory(InstancesFolder);
            var instanceConfiguration = InstanceFileName(instanceRecord.InstanceName);
            var instance = TryLoadInstanceConfiguration(instanceConfiguration) ??
                new Instance(instanceRecord.InstanceName, instanceRecord.ConfigurationFilePath);

            instance.ConfigurationFilePath = instanceRecord.ConfigurationFilePath;

            WriteInstanceConfiguration(instance, instanceConfiguration);
            log.Info("Saving instance: " + instance.Name);
        }

        public void DeleteInstance(string instanceName)
        {
            log.Info($"Inside Delete instance: {instanceName}");

            var instanceConfiguration = InstanceFileName(instanceName);

            try
            {
                log.Info($"Try Delete instance file: {instanceConfiguration}");
                fileSystem.DeleteFile(instanceConfiguration);
                log.Info($"Finished Deleting instance file: {instanceConfiguration}");
            }
            catch (UnauthorizedAccessException ex)
            {
                log.Warn(ex.Message);
                throw new ControlledFailureException($"Unable to delete file '{instanceConfiguration}' as user '{Environment.UserName}'. Please check file permissions.");
            }

            log.Info($"Deleting instance from registry: {instanceName}");
            
            registryApplicationInstanceStore.DeleteFromRegistry(instanceName);

            log.Info($"Deleted instance: {instanceName}");
        }

        public IList<ApplicationInstanceRecord> ListInstances()
        {
            var listFromRegistry = registryApplicationInstanceStore.GetListFromRegistry();
            var listFromFileSystem = new List<ApplicationInstanceRecord>();
            if (fileSystem.DirectoryExists(InstancesFolder))
                listFromFileSystem = fileSystem.EnumerateFiles(InstancesFolder)
                    .Select(LoadInstanceConfiguration)
                    .Select(instance => new ApplicationInstanceRecord(instance.Name, instance.ConfigurationFilePath))
                    .ToList();

            // for customers running multiple instances on a machine, they may have a version that only understood
            // using the registry. We need to list those too.
            var combinedInstanceList = listFromFileSystem
                .Concat(listFromRegistry.Where(x => listFromFileSystem.All(y => y.InstanceName != x.InstanceName)))
                .OrderBy(i => i.InstanceName);
            return combinedInstanceList.ToList();
        }

        internal void MigrateInstance(ApplicationInstanceRecord instanceRecord)
        {
            var instanceName = instanceRecord.InstanceName;
            if (File.Exists(InstanceFileName(instanceName)))
                return;

            var registryInstance = registryApplicationInstanceStore.GetInstanceFromRegistry(instanceName);
            if (registryInstance != null)
            {
                log.Info($"Migrating {applicationName} instance from registry - {instanceName}");
                try
                {
                    RegisterInstance(instanceRecord);
                }
                catch (Exception ex)
                {
                    log.Error(ex, "Error migrating instance data");
                    throw;
                }
            }
        }

        ApplicationInstanceRecord? GetNamedRegistryRecord(string instanceName)
        {
            var persistedApplicationInstanceRecords = this.ListInstances();

            var possibleNamedInstances = persistedApplicationInstanceRecords
                .Where(i => string.Equals(i.InstanceName, instanceName, StringComparison.InvariantCultureIgnoreCase))
                .ToArray();

            if (possibleNamedInstances.Length == 0)
                throw new ControlledFailureException($"Instance {instanceName} of {applicationName} has not been configured on this machine. Available instances: {AvailableInstancesText()}.");

            if (possibleNamedInstances.Length == 1 && possibleNamedInstances.Count() == 1)
            {
                var applicationInstanceRecord = possibleNamedInstances.First();
                return applicationInstanceRecord;
            }

            // to get more than 1, there must have been a match on differing case, try an exact case match
            var exactMatch = possibleNamedInstances.FirstOrDefault(x => x.InstanceName == instanceName);
            if (exactMatch == null) // null here means all matches were different case
                throw new ControlledFailureException($"Instance {instanceName} of {applicationName} could not be matched to one of the existing instances: {AvailableInstancesText()}.");
            return exactMatch;
        }

        ApplicationInstanceRecord? GetDefaultRegistryRecord()
        {
            var persistedApplicationInstanceRecords = ListInstances();
            // pick the default, if there is one
            var defaultInstance = persistedApplicationInstanceRecords.FirstOrDefault(i => i.InstanceName == ApplicationInstanceRecord.GetDefaultInstance(applicationName));
            if (defaultInstance != null)
            {
                return defaultInstance;
            }

            // if there is only a single instance, then pick it
            if (persistedApplicationInstanceRecords.Count == 1)
            {
                var singleInstance = persistedApplicationInstanceRecords.Single();
                return singleInstance;
            }

            if (persistedApplicationInstanceRecords.Count > 1)
                throw new ControlledFailureException($"There is more than one instance of {applicationName} configured on this machine. Please pass --instance=INSTANCENAME when invoking this command to target a specific instance. Available instances: {AvailableInstancesText()}.");

            return null;
        }

        string AvailableInstancesText()
        {
            return string.Join(", ",
                this.ListInstances()
                    .OrderBy(x => x.InstanceName, StringComparer.InvariantCultureIgnoreCase)
                    .Select(x => x.InstanceName));
        }

        void WriteInstanceConfiguration(Instance instance, string path)
        {
            var data = JsonConvert.SerializeObject(instance, Formatting.Indented);
            try
            {
                fileSystem.OverwriteFile(path, data);
            }
            catch (UnauthorizedAccessException ex)
            {
                log.Warn(ex.Message);
                throw new ControlledFailureException($"Unable to write file '{path}' as user '{Environment.UserName}'. Please check file permissions.");
            }
        }

        string InstanceFileName(string instanceName)
            => Path.Combine(InstancesFolder, instanceName.Replace(' ', '-').ToLower() + ".config");

        internal string InstancesFolder => Path.Combine(machineConfigurationHomeDirectory, applicationName.ToString(), "Instances");

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

        /// <summary>
        /// Serialized details of instance in stored in registry file
        /// </summary>
        class Instance
        {
            public Instance(string name, string configurationFilePath)
            {
                Name = name;
                ConfigurationFilePath = configurationFilePath;
            }

            public string Name { get; }
            public string ConfigurationFilePath { get; set; }
        }
    }
}
