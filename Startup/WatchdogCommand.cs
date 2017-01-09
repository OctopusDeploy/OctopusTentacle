using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Octopus.Shared.Configuration;
using Microsoft.Win32.TaskScheduler;
using Octopus.Diagnostics;

namespace Octopus.Shared.Startup
{
    public class WatchdogCommand : AbstractCommand
    {
        readonly ILog log;
        readonly ApplicationName applicationName;
        int interval = 5;
        bool createTask;
        bool deleteTask;
        HashSet<string> instances = new HashSet<string> { "*" };

        public WatchdogCommand(
            ILog log, 
            ApplicationName applicationName)
        {
            this.log = log;
            this.applicationName = applicationName;

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

            using (var taskService = new TaskService())
            {
                var taskName = "Octopus Watchdog " + applicationName;

                if (createTask)
                {
                    var taskDefinition = taskService.FindAllTasks(t => t.Name == taskName).SingleOrDefault()?.Definition;
                    if (taskDefinition == null)
                    {
                        taskDefinition = taskService.NewTask();

                        taskDefinition.Principal.UserId = "SYSTEM";
                        taskDefinition.Principal.LogonType = TaskLogonType.ServiceAccount;
                    }

                    taskDefinition.Actions.Clear();
                    taskDefinition.Actions.Add(new ExecAction(Assembly.GetEntryAssembly().Location, "checkservices --instances " + instanceNames + " --console --nologo", null));

                    taskDefinition.Triggers.Clear();
                    taskDefinition.Triggers.Add(new TimeTrigger
                    {
                        Repetition = new RepetitionPattern(TimeSpan.FromMinutes(interval), TimeSpan.Zero)
                    });

                    taskService.RootFolder.RegisterTaskDefinition(taskName, taskDefinition);
                }
                else if (deleteTask)
                {
                    if (taskService.FindAllTasks(t => t.Name == taskName).SingleOrDefault() != null)
                    {
                        taskService.RootFolder.DeleteTask(taskName);
                    }
                }
            }
        }
    }
}