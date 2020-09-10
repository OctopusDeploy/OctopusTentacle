using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration.Instances
{
    public class ConfigFileInstanceStrategy : IApplicationInstanceStrategy
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
            if (!(startUpInstanceRequest is StartUpConfigFileInstanceRequest request))
                return false;
            if (!fileSystem.FileExists(request.ConfigFile))
                throw new ArgumentException($"Specified config file {request.ConfigFile} not found.");
            return true;
        }

        public IList<ApplicationInstanceRecord> ListInstances()
        {
            if (!AnyInstancesConfigured())
                return Enumerable.Empty<ApplicationInstanceRecord>().ToList();

            var request = (StartUpConfigFileInstanceRequest)startUpInstanceRequest;
            return new List<ApplicationInstanceRecord>
            {
                new PersistedApplicationInstanceRecord(ConfigFileBasedInstanceName, request.ConfigFile, true)
            };
        }

        public ILoadedApplicationInstance LoadedApplicationInstance(ApplicationInstanceRecord applicationInstance)
        {
            var request = startUpInstanceRequest as StartUpConfigFileInstanceRequest;
            if (request == null)
                throw new ControlledFailureException("Configuration File was not specified");
            return new LoadedApplicationInstance(applicationInstance.InstanceName, new XmlFileKeyValueStore(fileSystem, request.ConfigFile));
        }
    }
}