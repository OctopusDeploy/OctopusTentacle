using System;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class LogInitializer : ILogInitializer
    {
        readonly ILoggingConfiguration configuration;
        readonly IOctopusFileSystem fileSystem;

        public LogInitializer(ILoggingConfiguration configuration, IOctopusFileSystem fileSystem)
        {
            this.configuration = configuration;
            this.fileSystem = fileSystem;
        }

        public void Start()
        {
            InitializeLogs();
        }

        void InitializeLogs()
        {
            var logDirectory = configuration.LogsDirectory;

            fileSystem.EnsureDirectoryExists(logDirectory);
            OctopusLogsDirectoryRenderer.LogsDirectory = logDirectory;
        }
    }
}