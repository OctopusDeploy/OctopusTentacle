using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s.Models;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Util;
using Octopus.Tentacle.Variables;

namespace Octopus.Tentacle.Scripts.Kubernetes
{
    public class RunningKubernetesJobScript : IRunningScript
    {
        private readonly IScriptWorkspace workspace;
        private readonly ScriptTicket scriptTicket;
        private readonly string taskId;
        private readonly ILog log;
        private readonly IKubernetesJobService jobService;
        private readonly IHomeConfiguration homeConfiguration;
        private readonly CancellationToken cancellationToken;

        public int ExitCode { get; private set; }
        public ProcessState State { get; private set; }
        public IScriptLog ScriptLog { get; }

        public RunningKubernetesJobScript(
            IScriptWorkspace workspace,
            IScriptLog scriptLog,
            ScriptTicket scriptTicket,
            string taskId,
            CancellationToken cancellationToken,
            ILog log,
            IKubernetesJobService jobService,
            IHomeConfiguration homeConfiguration)
        {
            this.workspace = workspace;
            this.scriptTicket = scriptTicket;
            this.taskId = taskId;
            this.log = log;
            this.jobService = jobService;
            this.homeConfiguration = homeConfiguration;
            this.cancellationToken = cancellationToken;
            ScriptLog = scriptLog;
        }

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
                            //create the k8s job
                            CreateJob();

                            State = ProcessState.Running;

                            //we now need to monitor the resulting pod status
                            exitCode = CheckIfPodHasCompleted().GetAwaiter().GetResult();
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

        private async Task<int> CheckIfPodHasCompleted()
        {
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return ScriptExitCodes.CanceledExitCode;
                }

                var job = jobService.TryGet(scriptTicket);
                if (job is null)
                    continue;

                var firstCondition = job.Status?.Conditions?.FirstOrDefault();
                switch (firstCondition)
                {
                    case { Status: "True", Type: "Complete" }:
                        return 0;
                    case { Status: "True", Type: "Failed" }:
                        return 1;
                    default:
                        await Task.Delay(50, cancellationToken);
                        break;
                }
            }
        }

        private void CreateJob()
        {
            var scriptName = Path.GetFileName(workspace.BootstrapScriptFilePath);

#if NETFX
            var applicationPath = Process.GetCurrentProcess().MainModule!.FileName;
#else
            var applicationPath = Environment.ProcessPath;
#endif

            var applicationDirectory = Path.GetDirectoryName(applicationPath);

            //TODO: Make the ScriptRunner a configurable location
            var scriptRunnerDirectory = Path.GetFullPath(Path.Combine(
                applicationDirectory!,
                "./../../../Octopus.Tentacle.Kubernetes.ScriptRunner/bin/net6.0/linux-x64"
            ));

            var job = new V1Job
            {
                ApiVersion = "batch/v1",
                Kind = "Job",
                Metadata = new V1ObjectMeta
                {
                    Name = jobService.BuildJobName(scriptTicket),
                    NamespaceProperty = KubernetesNamespace.Value,
                    Labels = new Dictionary<string, string>
                    {
                        ["octopus.com/serverTaskId"] = taskId,
                        ["octopus.com/scriptTicketId"] = scriptTicket.TaskId
                    }
                },
                Spec = new V1JobSpec
                {
                    Template = new V1PodTemplateSpec
                    {
                        Spec = new V1PodSpec
                        {
                            Containers = new List<V1Container>
                            {
                                new()
                                {
                                    Name = jobService.BuildJobName(scriptTicket),
                                    Image = "octopusdeploy/worker-tools:ubuntu.22.04",
                                    Command = new List<string> {"dotnet"},
                                    Args = new List<string>
                                    {
                                        "/data/tentacle-app/Octopus.Tentacle.Kubernetes.ScriptRunner.dll",
                                        "--script",
                                        $"\"/data/tentacle-work/{scriptName}\""
                                    }.Concat(
                                        (workspace.ScriptArguments ?? Array.Empty<string>())
                                        .SelectMany(arg => new[]
                                        {
                                            "--args",
                                            $"\"{arg}\""
                                        })
                                    ).ToList(),
                                    VolumeMounts = new List<V1VolumeMount>
                                    {
                                        new("/data/tentacle-work", "work"),
                                        new("/data/tentacle-app", "app"),
                                        new("/data/tentacle-home", "home")
                                    },
                                    Env = new List<V1EnvVar>
                                    {
                                        new(EnvironmentVariables.TentacleHome, "/data/tentacle-home"),
                                        new(EnvironmentVariables.TentacleVersion, Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleVersion)),
                                        new(EnvironmentVariables.TentacleCertificateSignatureAlgorithm, Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleCertificateSignatureAlgorithm)),
                                        new("OCTOPUS_RUNNING_IN_CONTAINER", "Y")
                                    }
                                }
                            },
                            RestartPolicy = "Never",
                            Volumes = new List<V1Volume>
                            {
                                new()
                                {
                                    Name = "work",
                                    HostPath = new V1HostPathVolumeSource(NormalizePathsForNix(workspace.WorkingDirectory))
                                },
                                new()
                                {
                                    Name = "app",
                                    HostPath = new V1HostPathVolumeSource(NormalizePathsForNix(scriptRunnerDirectory))
                                },
                                new()
                                {
                                    Name = "home",
                                    HostPath = new V1HostPathVolumeSource(NormalizePathsForNix(homeConfiguration.HomeDirectory))
                                }
                            }
                        }
                    },
                    BackoffLimit = 0, //we never want to rerun if it fails
                    TtlSecondsAfterFinished = 300 // 5min
                }
            };

            jobService.CreateJob(job);
        }

        private static string? NormalizePathsForNix(string? path)
        {
            if (path is null)
                return path;

            if (!PlatformDetection.IsRunningOnWindows)
                return path;

            var parts = path.Split('\\').ToList();

            //trim the volume colon and lowercase it
            parts[0] = parts[0].TrimEnd(':').ToLowerInvariant();

            //we insert an empty string because all paths start with /
            parts.Insert(0, string.Empty);

            return string.Join("/", parts);
        }
    }
}