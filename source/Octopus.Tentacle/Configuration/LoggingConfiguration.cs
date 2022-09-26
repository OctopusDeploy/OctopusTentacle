using System;
using System.IO;
using Octopus.Tentacle.Diagnostics;

namespace Octopus.Tentacle.Configuration
{
    public class LoggingConfiguration : ILoggingConfiguration
    {
        private readonly IHomeConfiguration home;

        public LoggingConfiguration(IHomeConfiguration home)
        {
            this.home = home;
        }

        public string LogsDirectory => home.HomeDirectory == null ? OctopusLogsDirectoryRenderer.LogsDirectory : Path.Combine(home.HomeDirectory, "Logs");
    }
}