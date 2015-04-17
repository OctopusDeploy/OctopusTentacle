using System;
using System.Diagnostics;
using Autofac;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class LogInitializer : ILogInitializer
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

            fileSystem.EnsureDirectoryExists(logDirectory);
            OctopusLogsDirectoryRenderer.LogsDirectory = logDirectory;
        }
    }
}