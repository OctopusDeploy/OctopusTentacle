using System;
using System.IO;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration.Instances
{
    class WorkingDirectoryConfigurationStrategy : IApplicationConfigurationStrategy
    {
        readonly IOctopusFileSystem fileSystem;
        readonly StartUpInstanceRequest startUpInstanceRequest;
        public int Priority => 0;

        public WorkingDirectoryConfigurationStrategy(
            
            
            IOctopusFileSystem fileSystem, StartUpInstanceRequest startUpInstanceRequest)
        {
            this.fileSystem = fileSystem;
            this.startUpInstanceRequest = startUpInstanceRequest;
        }
        
        public IAggregatableKeyValueStore? LoadedConfiguration(ApplicationRecord applicationInstance)
        {
            var configPath = ConfigPath();

            if (!fileSystem.FileExists(configPath))
                return null;

            return new XmlFileKeyValueStore(fileSystem, configPath);
        }

        string ConfigPath()
        {
            string currentDir = Environment.CurrentDirectory;
            
            // TODO: We should be able to pass through HomeDirectory via cmd line... next stage. for now its only current directory
            /*if (startUpInstanceRequest is StartUpDynamicInstanceRequest workingDirectoryStartArgs)
            {
                currentDir = workingDirectoryStartArgs.HomeDirectory;
            }*/

            var configPath = Path.Combine(currentDir, $"{startUpInstanceRequest.ApplicationName}.config");
            return configPath;
        }
    }
}