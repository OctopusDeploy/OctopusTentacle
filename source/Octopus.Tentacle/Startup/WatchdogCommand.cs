using System;
using System.Collections.Generic;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Util;
using Octopus.Tentacle.Watchdog;

namespace Octopus.Tentacle.Startup
{
    public class WatchdogCommand : AbstractCommand
    {
        readonly ISystemLog log;
        readonly ApplicationName applicationName;
        readonly Lazy<IWatchdog> watchdog;
        readonly IWindowsLocalAdminRightsChecker windowsLocalAdminRightsChecker;
        int interval = 5;
        bool createTask;
        bool deleteTask;
        HashSet<string> instances = new HashSet<string> { "*" };

        public WatchdogCommand(
            ISystemLog log,
            ApplicationName applicationName,
            Lazy<IWatchdog> watchdog,
            IWindowsLocalAdminRightsChecker windowsLocalAdminRightsChecker,
            ILogFileOnlyLogger logFileOnlyLogger)
            : base(logFileOnlyLogger)
        {
            this.log = log;
            this.applicationName = applicationName;
            this.watchdog = watchdog;
            this.windowsLocalAdminRightsChecker = windowsLocalAdminRightsChecker;

            Options.Add("create",
                "Create the watchdog task for the given instances",
                v =>
                {
                    createTask = true;
                    log.Info("Creating watchdog task");
                });
            Options.Add("delete",
                "TryDelete the watchdog task for the given instances",
                v =>
                {
                    deleteTask = true;
                    log.Info("Removing watchdog task");
                });
            Options.Add("interval=",
                "The interval, in minutes, at which that the service(s) should be checked (default: 5)",
                v =>
                {
                    log.Info($"Setting watchdog task interval to {v} minutes");
                    interval = int.Parse(v);
                });
            Options.Add("instances=",
                "Comma separated list of instances to be checked, or * to check all instances (default: *)",
                v =>
                {
                    instances = new HashSet<string>(v.Split(',', ';'));
                });
        }

        protected override void Start()
        {
            if (!PlatformDetection.IsRunningOnWindows)
                throw new ControlledFailureException("This command is only supported on Windows.");
            windowsLocalAdminRightsChecker.AssertIsRunningElevated();

            log.Info("ApplicationName: " + applicationName);
            var instanceNames = string.Join(",", instances);
            log.Info("Instances: " + instanceNames);

            if (deleteTask)
                watchdog.Value.Delete();
            else if (createTask)
                watchdog.Value.Create(instanceNames, interval);
            else
                throw new ControlledFailureException("Invalid arguments. Please specify either --create or --delete.");
        }
    }
}
