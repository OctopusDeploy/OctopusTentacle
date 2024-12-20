using System;
using System.IO;
using Octopus.Diagnostics;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Configuration.Instances
{
    class ApplicationInstanceManager : IApplicationInstanceManager
    {
        readonly IOctopusFileSystem fileSystem;
        readonly ISystemLog log;
        readonly IApplicationInstanceStore instanceStore;
        readonly IKubernetesAgentDetection kubernetesAgentDetection;
        readonly Lazy<IWritableHomeConfiguration> homeConfiguration;
        readonly ApplicationName applicationName;
        readonly StartUpInstanceRequest startUpInstanceRequest;

        public ApplicationInstanceManager(
            ApplicationName applicationName,
            StartUpInstanceRequest startUpInstanceRequest,
            IOctopusFileSystem fileSystem,
            ISystemLog log,
            IApplicationInstanceStore instanceStore,
            IKubernetesAgentDetection kubernetesAgentDetection,
            Lazy<IWritableHomeConfiguration> homeConfiguration)
        {
            this.applicationName = applicationName;
            this.startUpInstanceRequest = startUpInstanceRequest;
            this.fileSystem = fileSystem;
            this.log = log;
            this.instanceStore = instanceStore;
            this.kubernetesAgentDetection = kubernetesAgentDetection;
            this.homeConfiguration = homeConfiguration;
        }

        public void CreateDefaultInstance(string configurationFile, string? homeDirectory = null)
        {
            CreateInstance(ApplicationInstanceRecord.GetDefaultInstance(applicationName), configurationFile, homeDirectory);
        }

        public void CreateInstance(string instanceName, string configurationFile, string? homeDirectory = null)
        {
            (configurationFile, homeDirectory) = ValidateConfigDirectory(configurationFile, homeDirectory);
            EnsureConfigurationFileExists(configurationFile, homeDirectory);
            RegisterInstanceInIndex(instanceName, configurationFile);
            WriteHomeDirectory(homeDirectory);
        }

        public void DeleteInstance(string instanceName)
        {
            instanceStore.DeleteInstance(instanceName);
        }

        void RegisterInstanceInIndex(string instanceName, string configurationFile)
        {
            // If completely dynamic (no instance or configFile provided) then dont bother setting anything
            if (startUpInstanceRequest is StartUpDynamicInstanceRequest)
                return;

            var instance = new ApplicationInstanceRecord(instanceName, configurationFile);
            instanceStore.RegisterInstance(instance);
        }

        void WriteHomeDirectory(string homeDirectory)
        {
            log.Info($"Setting home directory to: {homeDirectory}");
            homeConfiguration.Value.SetHomeDirectory(homeDirectory);
        }

        void EnsureConfigurationFileExists(string configurationFile, string homeDirectory)
        {
            //Skip this step if we're running as a Kubernetes Agent
            if (kubernetesAgentDetection.IsRunningAsKubernetesAgent) return;

            // Ensure we can write configuration file
            var configurationDirectory = Path.GetDirectoryName(configurationFile) ?? homeDirectory;
            fileSystem.EnsureDirectoryExists(configurationDirectory);
            if (!fileSystem.FileExists(configurationFile))
            {
                log.Info("Creating empty configuration file: " + configurationFile);
                fileSystem.OverwriteFile(configurationFile, @"<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>");
            }
            else
            {
                log.Info($"Configuration file at {configurationFile} already exists.");
            }
        }

        (string ConfigurationFilePath, string HomeDirectory) ValidateConfigDirectory(string? configurationFile, string? homeDirectory)
        {
            // If home directory is not provided, we should try use the config file path if provided, otherwise fallback to cwd
            homeDirectory ??= (string.IsNullOrEmpty(configurationFile) ?
                "." :
                Path.GetDirectoryName(fileSystem.GetFullPath(configurationFile!)) ?? ".");

            // Current "Indexed" installs require configuration file to be provided.
            // We can therefore assume that if its missing, it will end up being created in the cwd
            var configurationFilePath = string.IsNullOrEmpty(configurationFile)
                ? $"{applicationName}.config"
                : configurationFile!;

            // If configuration File isn't rooted, root it to the home directory
            if (!Path.IsPathRooted(configurationFilePath))
            {
                configurationFilePath = Path.Combine(homeDirectory, configurationFilePath);
            }

            // get the configurationPath for writing, even if it is a relative path
            configurationFilePath = fileSystem.GetFullPath(configurationFilePath);

            return (configurationFilePath, homeDirectory);
        }
    }
}