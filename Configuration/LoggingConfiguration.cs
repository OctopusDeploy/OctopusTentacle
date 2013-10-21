using System;
using System.IO;
using Octopus.Platform.Deployment.Configuration;

namespace Octopus.Shared.Configuration
{
    public class LoggingConfiguration : ILoggingConfiguration
    {
        readonly IHomeConfiguration home;

        public LoggingConfiguration(IHomeConfiguration home)
        {
            this.home = home;
        }

        public string LogsDirectory
        {
            get { return Path.Combine(home.HomeDirectory, "Logs"); }
        }
    }
}