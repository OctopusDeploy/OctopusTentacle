using System;
using System.IO;
using Octopus.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration.Instances
{
    class ApplicationInstanceManager : IApplicationInstanceManager
    {
        readonly IOctopusFileSystem fileSystem;
        readonly ISystemLog log;
        readonly IApplicationInstanceRegistry instanceRegistry;
        readonly StartUpInstanceRequest startUpInstanceRequest;

        public ApplicationInstanceManager(StartUpInstanceRequest startUpInstanceRequest,
            IOctopusFileSystem fileSystem,
            ISystemLog log,
            IApplicationInstanceRegistry instanceRegistry)
        {
            this.startUpInstanceRequest = startUpInstanceRequest;
            this.fileSystem = fileSystem;
            this.log = log;
            this.instanceRegistry = instanceRegistry;
        }

        public ApplicationRecord? GetInstance(string instanceName)
            => instanceRegistry.GetInstance(instanceName);

        public void CreateDefaultInstance(string configurationFile, string? homeDirectory = null)
        {
            CreateInstance(ApplicationInstanceRecord.GetDefaultInstance(startUpInstanceRequest.ApplicationName), configurationFile, homeDirectory);
        }

        public void CreateInstance(string instanceName, string configurationFile, string? homeDirectory = null)
        {
            if (string.IsNullOrEmpty(configurationFile))
            {
                homeDirectory ??= Environment.CurrentDirectory;
                configurationFile = Path.Combine(homeDirectory, $"{startUpInstanceRequest.ApplicationName}.config");
            }

            var configurationDirectory = Path.GetDirectoryName(configurationFile) ?? throw new ArgumentException("Configuration file location must include directory information", nameof(configurationFile));
            homeDirectory ??= configurationDirectory;


            // Ensure we can write configuration file
            fileSystem.EnsureDirectoryExists(configurationDirectory);
            if (!fileSystem.FileExists(configurationFile))
            {
                log.Info("Creating empty configuration file: " + configurationFile);
                fileSystem.OverwriteFile(configurationFile, @"<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>");
            }
            
            // If completely dynamic (no registry) then dont bother setting anything
            if (!(startUpInstanceRequest is StartUpDynamicInstanceRequest))
            {
                var homeConfig = new WritableHomeConfiguration(startUpInstanceRequest.ApplicationName, new XmlFileKeyValueStore(fileSystem, configurationFile));
                log.Info($"Setting home directory to: {homeDirectory}");
                homeConfig.SetHomeDirectory(homeDirectory);
                
                var instance = new ApplicationInstanceRecord(instanceName, configurationFile);
                instanceRegistry.RegisterInstance(instance);
            }
        }

        public void DeleteInstance(string instanceName)
        {
            if (startUpInstanceRequest is StartUpDynamicInstanceRequest && string.IsNullOrEmpty(instanceName))
            {
                log.Warn($"No {startUpInstanceRequest.ApplicationName} instance available in the global registry to be removed. ");
            }
            else
            {
                instanceRegistry.DeleteInstance(instanceName);
            }
        }
    }
}