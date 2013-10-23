using System;
using System.Reflection;
using Autofac;
using Octopus.Platform.Deployment.Configuration;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Util;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Configuration
{
    public class LogInitializer : IStartable
    {
        readonly ILog log = Log.Octopus();
        readonly IApplicationInstanceSelector selector;
        readonly Lazy<ILoggingConfiguration> configuration;
        readonly IOctopusFileSystem fileSystem;

        public LogInitializer(IApplicationInstanceSelector selector, Lazy<ILoggingConfiguration> configuration, IOctopusFileSystem fileSystem)
        {
            this.selector = selector;
            this.configuration = configuration;
            this.fileSystem = fileSystem;
        }

        public void Start()
        {
            selector.Loaded += InitializeLogs;
            InitializeLogs();
        }

        void InitializeLogs()
        {
            if (selector.Current == null)
                return;

            selector.Loaded -= InitializeLogs;

            var logDirectory = configuration.Value.LogsDirectory;

            log.TraceFormat("Logs will be written to: {0}", logDirectory);
            fileSystem.EnsureDirectoryExists(logDirectory);

            OctopusLogsDirectoryRenderer.LogsDirectory = logDirectory;

            log.VerboseFormat("Octopus version: {0}", Assembly.GetExecutingAssembly().GetInformationalVersion());
        }
    }
}