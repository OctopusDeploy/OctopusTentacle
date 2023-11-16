using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s.Models;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Util;
using Octopus.Tentacle.Variables;

namespace Octopus.Tentacle.Scripts.Kubernetes
{
    public class RunningKubernetesJobScript : IRunningScript
    {
        readonly IScriptWorkspace workspace;
        readonly ScriptTicket scriptTicket;
        readonly string taskId;
        readonly ILog log;
        readonly IKubernetesJobService jobService;
        readonly KubernetesJobScriptExecutionContext executionContext;
        readonly CancellationToken scriptCancellationToken;
        readonly string? instanceName;

        public int ExitCode { get; private set; }
        public ProcessState State { get; private set; }
        public IScriptLog ScriptLog { get; }

        public RunningKubernetesJobScript(IScriptWorkspace workspace,
            IScriptLog scriptLog,
            ScriptTicket scriptTicket,
            string taskId,
            CancellationToken scriptCancellationToken,
            ILog log,
            IKubernetesJobService jobService,
            IApplicationInstanceSelector appInstanceSelector,
            KubernetesJobScriptExecutionContext executionContext)
        {
            this.workspace = workspace;
            this.scriptTicket = scriptTicket;
            this.taskId = taskId;
            this.log = log;
            this.jobService = jobService;
            this.executionContext = executionContext;
            this.scriptCancellationToken = scriptCancellationToken;
            ScriptLog = scriptLog;
            instanceName = appInstanceSelector.Current.InstanceName;
        }

        public async Task Execute(CancellationToken taskCancellationToken)
        {
            var exitCode = -1;

            var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(scriptCancellationToken, taskCancellationToken);
            var cancellationToken = linkedCancellationTokenSource.Token;
            try
            {
                using var writer = ScriptLog.CreateWriter();
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
                        await CreateJob(writer, cancellationToken);

                        State = ProcessState.Running;

                        //we now need to monitor the resulting pod status
                        exitCode = await CheckIfPodHasCompleted(cancellationToken);
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

        public void Complete()
        {
            jobService.Delete(scriptTicket);
        }

        async Task<int> CheckIfPodHasCompleted(CancellationToken cancellationToken)
        {
            var resultStatusCode = 0;
            await jobService.Watch(scriptTicket, job =>
            {
                var firstCondition = job.Status?.Conditions?.FirstOrDefault();
                switch (firstCondition)
                {
                    case { Status: "True", Type: "Complete" }:
                        resultStatusCode = 0;
                        return true;
                    case { Status: "True", Type: "Failed" }:
                        resultStatusCode = 1;
                        return true;
                    default:
                        //continue watching
                        return false;
                }
            }, ex =>
            {
                log.Error(ex);
                resultStatusCode = 0;
            }, cancellationToken);

            return resultStatusCode;
        }

        async Task CreateJob(IScriptLogWriter writer, CancellationToken cancellationToken)
        {
            var scriptName = Path.GetFileName(workspace.BootstrapScriptFilePath);

            var jobName = jobService.BuildJobName(scriptTicket);

            var job = new V1Job
            {
                ApiVersion = "batch/v1",
                Kind = "Job",
                Metadata = new V1ObjectMeta
                {
                    Name = jobName,
                    NamespaceProperty = KubernetesJobsConfig.Namespace,
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
                                    Name = jobName,
                                    Image = executionContext.ContainerImage,
                                    Command = new List<string> { "dotnet" },
                                    Args = new List<string>
                                    {
                                        "/data/tentacle-app/source/Octopus.Tentacle.Kubernetes.ScriptRunner/bin/net6.0/Octopus.Tentacle.Kubernetes.ScriptRunner.dll",
                                        "--script",
                                        $"\"/data/tentacle-home/{instanceName}/Work/{scriptTicket.TaskId}/{scriptName}\"",
                                        "--logToConsole"
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
                                        new("/data/tentacle-home", "tentacle-home"),
                                        new("/data/tentacle-app", "tentacle-app"),
                                    },
                                    Env = new List<V1EnvVar>
                                    {
                                        new(EnvironmentVariables.TentacleHome, $"/data/tentacle-home/{instanceName}"),
                                        new(EnvironmentVariables.TentacleVersion, Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleVersion)),
                                        new(EnvironmentVariables.TentacleCertificateSignatureAlgorithm, Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleCertificateSignatureAlgorithm)),
                                        new("OCTOPUS_RUNNING_IN_CONTAINER", "Y")
                                    }
                                }
                            },
                            ServiceAccountName = KubernetesJobsConfig.ServiceAccountName,
                            RestartPolicy = "Never",
                            Volumes = new List<V1Volume>
                            {
                                new()
                                {
                                    Name = "tentacle-home",
                                    PersistentVolumeClaim = new V1PersistentVolumeClaimVolumeSource("tentacle-home-pv-claim")
                                },
                                new()
                                {
                                    Name = "tentacle-app",
                                    PersistentVolumeClaim = new V1PersistentVolumeClaimVolumeSource("tentacle-app-pv-claim"),
                                },
                            }
                        }
                    },
                    BackoffLimit = 0, //we never want to rerun if it fails
                    TtlSecondsAfterFinished = 1800 //30min
                }
            };

            writer.WriteVerbose($"Executing script in Kubernetes Job '{job.Name()}'");

            await jobService.CreateJob(job, cancellationToken);
        }
    }
}