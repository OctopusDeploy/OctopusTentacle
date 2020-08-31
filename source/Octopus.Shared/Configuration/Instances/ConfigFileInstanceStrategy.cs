using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration.Instances
{
    public class ConfigFileInstanceStrategy : IVirtualApplicationInstanceStrategy
    {
        const string ConfigFileBasedInstanceName = "ConfigFileInstance";

        readonly StartUpInstanceRequest startUpInstanceRequest;
        readonly IOctopusFileSystem fileSystem;
        readonly IEnvFileLocator envFileLocator;

        public ConfigFileInstanceStrategy(StartUpInstanceRequest startUpInstanceRequest, IOctopusFileSystem fileSystem, IEnvFileLocator envFileLocator)
        {
            this.startUpInstanceRequest = startUpInstanceRequest;
            this.fileSystem = fileSystem;
            this.envFileLocator = envFileLocator;
        }

        public int Priority => 100;

        public bool AnyInstancesConfigured()
        {
            return startUpInstanceRequest is StartUpConfigFileInstanceRequest request && fileSystem.FileExists(request.ConfigFile);
        }

        public IList<ApplicationInstanceRecord> ListInstances()
        {
            if (!AnyInstancesConfigured())
                return Enumerable.Empty<ApplicationInstanceRecord>().ToList();
            return new List<ApplicationInstanceRecord>();
        }

        public LoadedApplicationInstance LoadedApplicationInstance(ApplicationInstanceRecord applicationInstance)
        {
            var request = startUpInstanceRequest as StartUpConfigFileInstanceRequest;
            if (request == null)
                throw new ControlledFailureException("Configuration File was not specified");
            return new LoadedApplicationInstance(applicationInstance.InstanceName, new XmlFileKeyValueStore(fileSystem, request.ConfigFile));
        }

        public ApplicationInstanceRecord? GetInstance()
        {
            if (!AnyInstancesConfigured())
                return null;
            var request = (StartUpConfigFileInstanceRequest)startUpInstanceRequest;
            return new PersistedApplicationInstanceRecord(ConfigFileBasedInstanceName, request.ConfigFile, true);
        }
    }
}