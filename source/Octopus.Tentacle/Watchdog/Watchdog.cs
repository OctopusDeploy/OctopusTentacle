using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Win32.TaskScheduler;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Watchdog
{
    public class Watchdog : IWatchdog
    {
        readonly ISystemLog log;
        readonly string taskName;
        readonly string argsPrefix = "checkservices --instances ";

        public Watchdog(ApplicationName applicationName, ISystemLog log)
        {
            taskName = "Octopus Watchdog " + applicationName;
            this.log = log;
        }

        public WatchdogConfiguration GetConfiguration()
        {
            var enabled = false;
            var interval = 0;
            var instances = "*";

            using (var taskService = new TaskService())
            {
                var taskDefinition = taskService.FindAllTasks(t => t.Name == taskName).SingleOrDefault()?.Definition;

                if (taskDefinition != null)
                {
                    enabled = true;
                    var trigger = taskDefinition.Triggers.FirstOrDefault(x => x is TimeTrigger);
                    if (trigger?.Repetition != null)
                        interval = (int)trigger.Repetition.Interval.TotalMinutes;
                    var action = taskDefinition.Actions.FirstOrDefault(x => x is ExecAction);
                    if (action != null)
                        instances = ((ExecAction)action).Arguments.Replace(argsPrefix, "");
                }
            }

            return new WatchdogConfiguration(enabled, interval, instances);
        }

        public void Delete()
        {
            using (var taskService = new TaskService())
            {
                if (taskService.FindAllTasks(t => t.Name == taskName).SingleOrDefault() == null)
                {
                    log.Info($"Scheduled task {taskName} not found. Nothing to do.");
                }
                else
                {
                    taskService.RootFolder.DeleteTask(taskName);
                    log.Info($"Deleted scheduled task {taskName}");
                }
            }
        }

        public void Create(string instanceNames, int interval)
        {
            using (var taskService = new TaskService())
            {
                var taskDefinition = taskService.FindAllTasks(t => t.Name == taskName).SingleOrDefault()?.Definition;
                if (taskDefinition == null)
                {
                    taskDefinition = taskService.NewTask();

                    taskDefinition.Principal.UserId = "SYSTEM";
                    taskDefinition.Principal.LogonType = TaskLogonType.ServiceAccount;
                    log.Info($"Creating scheduled task {taskName}");
                }
                else
                {
                    log.Info($"Updating scheduled task {taskName}");
                }

                var entryAssembly = Assembly.GetEntryAssembly() ?? throw new Exception("Could not get entry assembly");
                var fileName = entryAssembly.GetName().Name;
                var processFileName = Process.GetCurrentProcess().MainModule?.FileName;

                if (processFileName == null || !Path.GetFileNameWithoutExtension(processFileName).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    processFileName = Path.Combine(Path.GetDirectoryName(entryAssembly.Location) ?? ".", $"{Path.GetFileNameWithoutExtension(entryAssembly.Location)}.exe");

                taskDefinition.Actions.Clear();
                taskDefinition.Actions.Add(new ExecAction(processFileName, argsPrefix + instanceNames));

                taskDefinition.Triggers.Clear();
                taskDefinition.Triggers.Add(new TimeTrigger
                {
                    Repetition = new RepetitionPattern(TimeSpan.FromMinutes(interval), TimeSpan.Zero)
                });

                var task = taskService.RootFolder.RegisterTaskDefinition(taskName, taskDefinition);
                task.Enabled = true;
            }
        }
    }
}