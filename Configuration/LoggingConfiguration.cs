using System;
using System.IO;
using System.Reflection;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class LoggingConfiguration : ILoggingConfiguration, Autofac.IStartable
    {
        readonly ILog log = Log.Octopus();
        readonly IHomeConfiguration home;
        readonly IOctopusFileSystem fileSystem;

        public LoggingConfiguration(IHomeConfiguration home, IOctopusFileSystem fileSystem)
        {
            this.home = home;
            this.fileSystem = fileSystem;
        }

        public string LogsDirectory
        {
            get { return Path.Combine(home.HomeDirectory, "Logs"); }
        }

        public void Start()
        {
            var logDirectory = LogsDirectory;

            log.TraceFormat("Logs will be written to: {0}", logDirectory);
            fileSystem.EnsureDirectoryExists(logDirectory);

            OctopusLogsDirectoryRenderer.LogsDirectory = logDirectory;

            log.InfoFormat("Octopus version: {0}", Assembly.GetExecutingAssembly().GetInformationalVersion());
        }
    }
}