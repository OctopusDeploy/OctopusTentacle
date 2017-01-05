using System;
using System.Linq;
using System.Reflection;
using Octopus.Shared.Configuration;
using Microsoft.Win32.TaskScheduler;
using Octopus.Diagnostics;

namespace Octopus.Shared.Startup
{
    public class WatchdogCommand : AbstractStandardCommand
    {
        readonly IApplicationInstanceSelector instanceSelector;
        int interval = 5;
        bool createTask;
        bool deleteTask;

        public WatchdogCommand(ILog log, IApplicationInstanceSelector instanceSelector) : base(instanceSelector)
        {
            this.instanceSelector = instanceSelector;
            Options.Add("create", "Create the watchdog task for the given instance", v =>
            {
                createTask = true;
                log.Info("Creating watchdog task");
            });
            Options.Add("delete", "Delete the watchdog task for the given instance", v =>
            {
                deleteTask = true;
                log.Info("Removing watchdog task");
            });
            Options.Add("interval=", "The interval, in minutes, that the service should be checked", v =>
            {
                log.Info($"Setting watchdog task interval to {v} minutes");
                interval = int.Parse(v);
            });
        }

        protected override void Start()
        {
            base.Start();

            using (var taskService = new TaskService())
            {
                var instanceName = instanceSelector.Current.InstanceName;
                var taskName = "Octopus Watchdog " + instanceName;

                if (createTask)
                {
                    var taskDefinition = taskService.FindAllTasks(t => t.Name == taskName).SingleOrDefault()?.Definition;
                    if (taskDefinition == null)
                    {
                        taskDefinition = taskService.NewTask();

                        taskDefinition.Actions.Add(new ExecAction(Assembly.GetEntryAssembly().Location, "service --start --instance " + instanceName + " --console", null));

                        taskDefinition.Principal.UserId = "SYSTEM";
                        taskDefinition.Principal.LogonType = TaskLogonType.ServiceAccount;
                    }

                    taskDefinition.Triggers.Clear();
                    taskDefinition.Triggers.Add(new BootTrigger()
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