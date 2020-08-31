using System;

namespace Octopus.Shared.Configuration
{
    public class StartUpConfigFileInstanceRequest : StartUpDynamicInstanceRequest
    {
        public StartUpConfigFileInstanceRequest(ApplicationName applicationName, string configFile) : base(applicationName)
        {
            ConfigFile = configFile;
        }

        public string ConfigFile { get; }
    }
}