using Octopus.Shared.Diagnostics;
using Octopus.Shared.Startup;

namespace Octopus.Shared.Configuration
{
    internal class LogInitializer
    {
        readonly ILoggingConfiguration configuration;

        public LogInitializer(ILoggingConfiguration configuration)
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

            var previousLogDirectory = OctopusLogsDirectoryRenderer.LogsDirectory;
            if (previousLogDirectory != logDirectory)
            {
                //log to the old log file that we are now logging somewhere else
                LogFileOnlyLogger.Info($"Changing log folder from {previousLogDirectory} to {logDirectory}");

                OctopusLogsDirectoryRenderer.SetLogsDirectory(logDirectory);

                //log to the new log file that we were logging somewhere else
                LogFileOnlyLogger.Info(new string('=', 80));
                LogFileOnlyLogger.Info($"Changed log folder from {previousLogDirectory} to {logDirectory}");
            }
        }
    }
}