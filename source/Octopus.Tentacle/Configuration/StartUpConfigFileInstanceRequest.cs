using System;

namespace Octopus.Tentacle.Configuration
{
    public class StartUpConfigFileInstanceRequest : StartUpInstanceRequest
    {
        public StartUpConfigFileInstanceRequest(string configFile)
        {
            ConfigFile = configFile;
        }

        public string ConfigFile { get; }
    }
}