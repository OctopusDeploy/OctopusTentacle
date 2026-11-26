using System;
using System.IO;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Core.Util;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Configuration.Instances
{
    class ApplicationInstanceManager : IApplicationInstanceManager
    {
        readonly IOctopusFileSystem fileSystem;
        readonly ISystemLog log;
        readonly IApplicationInstanceStore instanceStore;
        readonly Lazy<IWritableHomeConfiguration> homeConfiguration;
        readonly ApplicationName applicationName;
        readonly StartUpInstanceRequest startUpInstanceRequest;

        public ApplicationInstanceManager(
            ApplicationName applicationName,
            StartUpInstanceRequest startUpInstanceRequest,
            IOctopusFileSystem fileSystem,
            ISystemLog log,
            IApplicationInstanceStore instanceStore,
            Lazy<IWritableHomeConfiguration> homeConfiguration)
        {
            this.applicationName = applicationName;
            this.startUpInstanceRequest = startUpInstanceRequest;
            this.fileSystem = fileSystem;
            this.log = log;
            this.instanceStore = instanceStore;
            this.homeConfiguration = homeConfiguration;
        }

        public void CreateDefaultInstance(string configurationFile, string? homeDirectory = null)
        {
            CreateInstance(ApplicationInstanceRecord.GetDefaultInstance(applicationName), configurationFile, homeDirectory);
        }

        public void CreateInstance(string instanceName, string configurationFile, string? homeDirectory = null)
        {
            (configurationFile, homeDirectory) = ValidateConfigDirectory(configurationFile, homeDirectory);
            EnsureConfigurationEncryptionKeyExists(instanceName);
            EnsureConfigurationFileExists(configurationFile, homeDirectory);
            RegisterInstanceInIndex(instanceName, configurationFile);
            WriteHomeDirectory(homeDirectory);
        }

        public void DeleteInstance(string instanceName)
        {
            instanceStore.DeleteInstance(instanceName);
        }

        // Created before anything else, so that a failure leaves no half-made instance behind.
        void EnsureConfigurationEncryptionKeyExists(string instanceName)
        {
            if (PlatformDetection.IsRunningOnWindows || KubernetesSupportDetection.IsRunningAsKubernetesAgent || startUpInstanceRequest is StartUpDynamicInstanceRequest)
            {
                return;
            }

            var keyFilePath = ConfigurationEncryptionKey.KeyFilePathFor(instanceName);
            var key = new ConfigurationEncryptionKey(log, fileSystem, keyFilePath);
            try
            {
                key.CreateIfMissing();
            }
            catch (Exception e)
            {
                var user = Environment.UserName;
                var directory = key.KeyDirectory;
                throw new ControlledFailureException($"Unable to create the configuration encryption key `{keyFilePath}` for the instance `{instanceName}`, so the instance was not created: {e.Message} "
                    + $"Run this command as root, or give the user ({user}) access to the directory, for example: `sudo sh -c 'umask 022 && mkdir -p {directory} && chown {user} {directory} && chmod 700 {directory}'`.", e);
            }
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
            //Skip this step if we're running on Kubernetes
            if (KubernetesSupportDetection.IsRunningAsKubernetesAgent) return;

            // Ensure we can write configuration file
            string configurationDirectory = Path.GetDirectoryName(configurationFile) ?? homeDirectory;
            fileSystem.EnsureDirectoryExists(configurationDirectory);
            if (!fileSystem.FileExists(configurationFile))
            {
                log.Info("Creating empty configuration file: " + configurationFile);
                ConfigurationFilePermissions.CreateEmpty(fileSystem, configurationFile, @"<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>");
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