using System;
using Octopus.Configuration;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration.Instances
{
    public class ConfigFileConfigurationStrategy : IApplicationConfigurationStrategy
    {
        readonly StartUpInstanceRequest startUpInstanceRequest;
        readonly IOctopusFileSystem fileSystem;

        public ConfigFileConfigurationStrategy(StartUpInstanceRequest startUpInstanceRequest, IOctopusFileSystem fileSystem)
        {
            this.startUpInstanceRequest = startUpInstanceRequest;
            this.fileSystem = fileSystem;
        }

        public int Priority => 500;

        public IKeyValueStore? LoadedConfiguration(ApplicationInstanceRecord applicationInstance)
        {
            var request = startUpInstanceRequest as StartUpConfigFileInstanceRequest;
            if (request == null)
                return null;
            if (!fileSystem.FileExists(request.ConfigFile))
                throw new ArgumentException($"Specified config file {request.ConfigFile} not found.");
            return new XmlFileKeyValueStore(fileSystem, request.ConfigFile);
        }
    }
}