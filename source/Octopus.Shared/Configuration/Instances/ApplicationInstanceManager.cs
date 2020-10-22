using System;
using System.IO;
using Octopus.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration.Instances
{
    class ApplicationInstanceManager : IApplicationInstanceManager
    {
        readonly IOctopusFileSystem fileSystem;
        readonly ILog log;
        readonly IApplicationInstanceStore instanceStore;

        public ApplicationInstanceManager(ApplicationName applicationName,
            IOctopusFileSystem fileSystem,
            ILog log,
            IApplicationInstanceStore instanceStore)
        {
            ApplicationName = applicationName;
            this.fileSystem = fileSystem;
            this.log = log;
            this.instanceStore = instanceStore;
        }

        public ApplicationName ApplicationName { get; }

        public ApplicationInstanceRecord? GetInstance(string instanceName)
        {
            return instanceStore.GetInstance(instanceName);
        }

        public void CreateDefaultInstance(string configurationFile, string? homeDirectory = null)
        {
            CreateInstance(ApplicationInstanceRecord.GetDefaultInstance(ApplicationName), configurationFile, homeDirectory);
        }

        public void CreateInstance(string instanceName, string configurationFile, string? homeDirectory = null)
        {
            var parentDirectory = Path.GetDirectoryName(configurationFile);
            fileSystem.EnsureDirectoryExists(parentDirectory);

            if (!fileSystem.FileExists(configurationFile))
            {
                log.Info("Creating empty configuration file: " + configurationFile);
                fileSystem.OverwriteFile(configurationFile, @"<?xml version='1.0' encoding='UTF-8' ?><octopus-settings></octopus-settings>");
            }

            var instance = new ApplicationInstanceRecord(instanceName, configurationFile);
            instanceStore.SaveInstance(instance);

            var homeConfig = new WritableHomeConfiguration(ApplicationName, new XmlFileKeyValueStore(fileSystem, configurationFile));
            var home = !string.IsNullOrWhiteSpace(homeDirectory) ? homeDirectory : parentDirectory;
            log.Info($"Setting home directory to: {home}");
            homeConfig.SetHomeDirectory(home);
        }

        public void DeleteInstance(string instanceName)
        {
            instanceStore.DeleteInstance(instanceName);
        }
    }
}