using System;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class LogInitializer : ILogInitializer
    {
        readonly ILoggingConfiguration configuration;

        public LogInitializer(ILoggingConfiguration configuration, IOctopusFileSystem fileSystem)
        {
            this.configuration = configuration;
        }

        public void Start()
        {
            InitializeLogs();
        }

        void InitializeLogs()
        {
            // If the LogsDirectory isn't configured yet (probably because the HomeDirectory isn't configured yet) continue logging to the fallback directory
            var logDirectory = configuration.LogsDirectory;
            if (logDirectory == null) return;

            OctopusLogsDirectoryRenderer.SetLogsDirectory(logDirectory);
        }
    }
}