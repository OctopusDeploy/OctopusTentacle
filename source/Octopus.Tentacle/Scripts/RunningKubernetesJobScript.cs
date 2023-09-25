﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Octopus.Diagnostics;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Scripts
{
    public class RunningKubernetesJobScript : IRunningScript
    {
        private readonly IShell shell;
        private readonly IScriptWorkspace workspace;
        private readonly ScriptTicket ticket;
        private readonly string taskId;
        private readonly CancellationToken cancellationToken;
        private readonly ILog log;

        private static readonly object OutputFileLock = new();

        private static readonly Dictionary<string, ProcessOutputSource> FileSourceMap = new Dictionary<string, ProcessOutputSource>
        {
            ["job-output.log"] = ProcessOutputSource.StdOut,
            ["job-error.log"] = ProcessOutputSource.StdErr
        };

        private readonly Dictionary<string, long> lastFilePositions;

        public RunningKubernetesJobScript(IShell shell,
            IScriptWorkspace workspace,
            IScriptLog scriptLog,
            ScriptTicket ticket,
            string taskId,
            CancellationToken cancellationToken,
            ILog log)
        {
            this.shell = shell;
            this.workspace = workspace;
            this.ticket = ticket;
            this.taskId = taskId;
            this.cancellationToken = cancellationToken;
            this.log = log;
            ScriptLog = scriptLog;
            lastFilePositions = FileSourceMap.ToDictionary(kvp => kvp.Key, _ => 0L);
        }

        public int ExitCode { get; private set; }
        public ProcessState State { get; private set; }
        public IScriptLog ScriptLog { get; }

        public void Execute()
        {
            var exitCode = -1;
            try
            {
                using (var writer = ScriptLog.CreateWriter())
                {
                    try
                    {
                        using (ScriptIsolationMutex.Acquire(workspace.IsolationLevel,
                                   workspace.ScriptMutexAcquireTimeout,
                                   workspace.ScriptMutexName ?? nameof(RunningKubernetesJobScript),
                                   message => writer.WriteOutput(ProcessOutputSource.StdOut, message),
                                   taskId,
                                   cancellationToken,
                                   log))
                        {
                            State = ProcessState.Running;

                            CreateJobBootstrapScript();

                            var path = CreateJobYaml();

                            exitCode = RunAndMonitorKubernetesJob(path, writer);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        writer.WriteOutput(ProcessOutputSource.StdOut, "Script execution canceled.");
                        exitCode = ScriptExitCodes.CanceledExitCode;
                    }
                    catch (TimeoutException)
                    {
                        writer.WriteOutput(ProcessOutputSource.StdOut, "Script execution timed out.");
                        exitCode = ScriptExitCodes.TimeoutExitCode;
                    }
                }
            }
            catch (Exception)
            {
                exitCode = ScriptExitCodes.FatalExitCode;
            }
            finally
            {
                ExitCode = exitCode;
                State = ProcessState.Complete;
            }
        }

        private int RunAndMonitorKubernetesJob(string path, IScriptLogWriter writer)
        {
            var commandLine = new CommandLineInvocation(
                "kubectl",
                $"apply -f \"{path}\"");
            var result = commandLine.ExecuteCommand();

            // if creating the job failed, blow up
            if (result.ExitCode != 0)
                return result.ExitCode;

            //otherwise, we now need to monitor the resulting output files and pod status
            MonitorPodAndOutputFiles(writer).GetAwaiter().GetResult();

            return result.ExitCode;
        }

        private string CreateJobYaml()
        {
            var jobYaml = BuildJobYaml();

            var path = workspace.WriteFile("KubernetesJob.yaml", jobYaml);
            return path;
        }

        private async Task MonitorPodAndOutputFiles(IScriptLogWriter writer)
        {
            using var completionReset = new ManualResetEventSlim(false);

            var outputMonitorTask = Task.Run(() => MonitorOutputFiles(writer, completionReset), cancellationToken);
            var jobMonitorTask = Task.Run(async () => await CheckIfPodHasCompleted(completionReset), cancellationToken);

            await Task.WhenAll(jobMonitorTask, outputMonitorTask);

            //once the monitoring jobs are finished, we finish off the output files
            foreach (var file in FileSourceMap.Keys)
            {
                WriteOutputFromFileChange(writer, new FileSystemEventArgs(WatcherChangeTypes.Changed, workspace.WorkingDirectory, file));
            }
        }

        private async Task<int> CheckIfPodHasCompleted(ManualResetEventSlim resetEvent)
        {
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    resetEvent.Set();
                    return ScriptExitCodes.CanceledExitCode;
                }

                var kubectlCheckCommand = new CommandLineInvocation(
                    "kubectl",
                    $"get job octopus-tentacle-{ticket.TaskId} --namespace octopus-tentacle -o jsonpath=\"{{.status.conditions[].type}}\"");
                var result = kubectlCheckCommand.ExecuteCommand(workspace.WorkingDirectory);

                var firstLine = result.Infos.FirstOrDefault();
                if (firstLine != null)
                {
                    switch (firstLine)
                    {
                        case "Complete":
                            resetEvent.Set();
                            return 0;
                        case "Failed":
                            resetEvent.Set();
                            return 1;
                        default:
                            await Task.Delay(50, cancellationToken);
                            break;
                    }
                }
            }
        }

        private void MonitorOutputFiles(IScriptLogWriter writer, ManualResetEventSlim resetEvent)
        {
            var watcher = new FileSystemWatcher();
            watcher.Path = workspace.WorkingDirectory;
            watcher.EnableRaisingEvents = true;
            watcher.NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.Size;

            watcher.Created += OnFileWatcherEventHandler;
            watcher.Changed += OnFileWatcherEventHandler;

            resetEvent.Wait(cancellationToken);

            //stop watching the events
            watcher.Created -= OnFileWatcherEventHandler;
            watcher.Changed -= OnFileWatcherEventHandler;
            return;

            void OnFileWatcherEventHandler(object obj, FileSystemEventArgs args)
            {
                WriteOutputFromFileChange(writer, args, resetEvent);
            }
        }

        private void WriteOutputFromFileChange(IScriptLogWriter writer, FileSystemEventArgs eventArgs, ManualResetEventSlim? resetEvent = null)
        {
            lock (OutputFileLock)
            {
                //this is not a file we are watching
                if (eventArgs.Name == null || !FileSourceMap.ContainsKey(eventArgs.Name))
                    return;

                //the file doesn't exist (for some reason)
                if (!File.Exists(eventArgs.FullPath))
                    return;

                if (resetEvent is { IsSet: true })
                {
                    return;
                }

                if (!lastFilePositions.TryGetValue(eventArgs.Name, out var lastPosition))
                {
                    lastPosition = 0;
                    lastFilePositions[eventArgs.Name] = lastPosition;
                }

                using var fileStream = new FileStream(eventArgs.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                fileStream.Seek(lastPosition, SeekOrigin.Begin);

                using var reader = new StreamReader(fileStream);
                var remaining = reader.ReadToEnd();

                var lines = remaining.Split(Environment.NewLine.ToCharArray());
                var source = FileSourceMap[eventArgs.Name];
                foreach (var line in lines)
                {
                    writer.WriteOutput(source, line);
                }

                lastFilePositions[eventArgs.Name] = fileStream.Position;
            }
        }
        private void CreateJobBootstrapScript()
        {
            var filename = Path.GetFileName(workspace.BootstrapScriptFilePath);

            var args = new List<string>
            {
                $"/data/work/{ticket.TaskId}/{filename}"
            };

            args.AddRange(workspace.ScriptArguments ?? Array.Empty<string>());

            args.AddRange(new[]
            {
                ">>",
                $"/data/work/{ticket.TaskId}/job-output.log",
                "2>>",
                $"/data/work/{ticket.TaskId}/job-error.log"
            });

            var escapedArgs = args
                .Select(arg => $"\"{arg}\"")
                .ToList();

            var scriptArgs = string.Join(" ", args);

            var script = $@"#!/bin/bash
/bin/bash {scriptArgs}";

            workspace.WriteFile("JobBootstrap.sh", script);
        }

        private string BuildJobYaml()
        {
            //We normalize the workspace path as it's going to be running in linux
            var normalizedWorkspace = NormalizeWorkingDirectoryForNix(workspace.WorkingDirectory);

            return @$"apiVersion: batch/v1
kind: Job
metadata:
    name: ""octopus-tentacle-{ticket.TaskId}""
    namespace: ""octopus-tentacle""
    labels: 
        ""serverTaskId"": ""{taskId}""
spec:
    template:
        spec:
            containers:
            - name: ""octopus-tentacle-{ticket.TaskId}""
              image: octopusdeploy/worker-tools:ubuntu.22.04
              command: [""{Bash.GetFullBashPath()}""]
              args: [""/data/work/{ticket.TaskId}/JobBootstrap.sh""]
              volumeMounts:
                - mountPath: ""/data/work/{ticket.TaskId}""
                  name: work
            restartPolicy: Never
            volumes:
              - name: work
                hostPath:
                  path: ""{normalizedWorkspace}""
              - name: app
                hostPath:
                  path: ""{normalizedWorkspace}""
    backoffLimit: 0
";
        }

        private static string NormalizeWorkingDirectoryForNix(string workingDirectory)
        {
            if (!PlatformDetection.IsRunningOnWindows)
                return workingDirectory;

            var parts = workingDirectory.Split('\\').ToList();

            //trim the volume colon and lowercase it
            parts[0] = parts[0].TrimEnd(':').ToLowerInvariant();

            //we insert an empty string because all paths start with /
            parts.Insert(0, string.Empty);

            return string.Join("/", parts);
        }
    }
}