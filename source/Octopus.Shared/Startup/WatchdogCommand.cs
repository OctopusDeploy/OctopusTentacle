using System;
using System.Collections.Generic;
using Octopus.Shared.Configuration;
using Octopus.Diagnostics;
using Octopus.Shared.Services;

namespace Octopus.Shared.Startup
{
    public class WatchdogCommand : AbstractCommand
    {
        readonly ILog log;
        readonly ApplicationName applicationName;
        readonly Lazy<IWatchdog> watchdog;
        int interval = 5;
        bool createTask;
        bool deleteTask;
        HashSet<string> instances = new HashSet<string> { "*" };

        public WatchdogCommand(
            ILog log, 
            ApplicationName applicationName,
            Lazy<IWatchdog> watchdog)
        {
            this.log = log;
            this.applicationName = applicationName;
            this.watchdog = watchdog;

            Options.Add("create", "Create the watchdog task for the given instances", v =>
            {
                createTask = true;
                log.Info("Creating watchdog task");
            });
            Options.Add("delete", "Delete the watchdog task for the given instances", v =>
            {
                deleteTask = true;
                log.Info("Removing watchdog task");
            });
            Options.Add("interval=", "The interval, in minutes, at which that the service(s) should be checked (default: 5)", v =>
            {
                log.Info($"Setting watchdog task interval to {v} minutes");
                interval = int.Parse(v);
            });
            Options.Add("instances=", "List of instances to be checked (default: *)", v =>
            {
                instances = new HashSet<string>(v.Split(',', ';'));
            });
        }

        protected override void Start()
        {
            log.Info("ApplicationName: " + applicationName);
            var instanceNames = string.Join(",", instances);
            log.Info("Instances: " + instanceNames);

            if (deleteTask)
            {
                watchdog.Value.Delete();
            }
            if (createTask)
            {
                watchdog.Value.Create(instanceNames, interval);
            }
        }
    }
}
