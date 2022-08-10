using System;
using System.IO;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Configuration
{
    class LogInitializer
    {
        readonly ILoggingConfiguration configuration;
        readonly ILogFileOnlyLogger logFileOnlyLogger;

        public LogInitializer(ILoggingConfiguration configuration, ILogFileOnlyLogger logFileOnlyLogger)
        {
            this.configuration = configuration;
            this.logFileOnlyLogger = logFileOnlyLogger;
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

            var previousLogDirectory = OctopusLogsDirectoryRenderer.LogsDirectory;
            if (previousLogDirectory != logDirectory)
            {
                //log to the old log file that we are now logging somewhere else
                logFileOnlyLogger.Info($"Changing log folder from {previousLogDirectory} to {logDirectory}");

                if (string.IsNullOrEmpty(logDirectory)) throw new ArgumentException("Value cannot be null or empty.", nameof(logDirectory));

                if (!Directory.Exists(logDirectory))
                    Directory.CreateDirectory(logDirectory);

                OctopusLogsDirectoryRenderer.History.Add(logDirectory);
                OctopusLogsDirectoryRenderer.LogsDirectory = logDirectory;

                //log to the new log file that we were logging somewhere else
                logFileOnlyLogger.Info(new string('=', 80));
                logFileOnlyLogger.Info($"Changed log folder from {previousLogDirectory} to {logDirectory}");
            }
        }
    }
}