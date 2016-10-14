using System;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class LogInitializer : ILogInitializer
    {
        readonly Lazy<ILoggingConfiguration> configuration;
        readonly IOctopusFileSystem fileSystem;

        public LogInitializer(Lazy<ILoggingConfiguration> configuration, IOctopusFileSystem fileSystem)
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
            var logDirectory = configuration.Value.LogsDirectory;

            fileSystem.EnsureDirectoryExists(logDirectory);
            OctopusLogsDirectoryRenderer.LogsDirectory = logDirectory;
        }
    }
}