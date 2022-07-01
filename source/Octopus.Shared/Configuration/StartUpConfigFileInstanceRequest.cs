using System;

namespace Octopus.Shared.Configuration
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