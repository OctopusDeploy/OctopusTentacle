using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Nito.AsyncEx;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Util;
using Octopus.Tentacle.Variables;

namespace Octopus.Tentacle.Scripts.Kubernetes
{
    public class RunningKubernetesJobScript : IRunningScript
    {
        static readonly AsyncLazy<string> BootstrapRunnerScript = new(async () =>
        {
            using var stream = typeof(RunningKubernetesJobScript).Assembly.GetManifestResourceStream("Octopus.Tentacle.Kubernetes.bootstrapRunner.sh");
            using var reader = new StreamReader(stream!, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        });

        readonly IScriptWorkspace workspace;
        readonly ScriptTicket scriptTicket;
        readonly string taskId;
        readonly ILog log;
        readonly IScriptStateStore stateStore;
        readonly IKubernetesJobService jobService;
        readonly IKubernetesJobContainerResolver containerResolver;
        readonly CancellationToken scriptCancellationToken;
        readonly string? instanceName;
        readonly KubernetesJobOutputStreamWriter outputStreamWriter;

        public int ExitCode { get; private set; }
        public ProcessState State { get; private set; }
        public IScriptLog ScriptLog { get; }

        public RunningKubernetesJobScript(
            IScriptWorkspace workspace,
            IScriptLog scriptLog,
            ScriptTicket scriptTicket,
            string taskId,
            CancellationToken scriptCancellationToken,
            ILog log,
            IScriptStateStore stateStore,
            IKubernetesJobService jobService,
            IKubernetesJobContainerResolver containerResolver,
            IApplicationInstanceSelector appInstanceSelector)
        {
            this.workspace = workspace;
            this.scriptTicket = scriptTicket;
            this.taskId = taskId;
            this.log = log;
            this.stateStore = stateStore;
            this.jobService = jobService;
            this.containerResolver = containerResolver;
            this.scriptCancellationToken = scriptCancellationToken;
            ScriptLog = scriptLog;
            instanceName = appInstanceSelector.Current.InstanceName;

            outputStreamWriter = new KubernetesJobOutputStreamWriter(workspace);
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
                        RecordScriptHasStarted(writer);

                        //we now need to monitor the resulting pod status
                        exitCode = await MonitorJobAndLogs(writer, cancellationToken);
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
                try
                {
                    RecordScriptHasCompleted(exitCode);
                }
                finally
                {
                    ExitCode = exitCode;
                    State = ProcessState.Complete;
                }
            }
        }

        async Task<int> MonitorJobAndLogs(IScriptLogWriter writer, CancellationToken cancellationToken)
        {
            var jobCompletionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var checkJobTask = CheckIfJobHasCompleted(cancellationToken, jobCompletionCancellationTokenSource);

            //we pass the job completion CTS here because its used to cancel the writing of the job stream
            var monitorJobOutputTask = outputStreamWriter.StreamJobLogsToScriptLog(writer, jobCompletionCancellationTokenSource.Token);

            await Task.WhenAll(checkJobTask, monitorJobOutputTask);

            //once they have both finished, perform one last log read (and don't cancel on it)
            await outputStreamWriter.StreamJobLogsToScriptLog(writer, CancellationToken.None, true);

            //return the exit code of the jobs
            return checkJobTask.Result;
        }

        async Task<int> CheckIfJobHasCompleted(CancellationToken cancellationToken, CancellationTokenSource jobCompletionCancellationTokenSource)
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

            jobCompletionCancellationTokenSource.Cancel();

            return resultStatusCode;
        }



        async Task CreateJob(IScriptLogWriter writer, CancellationToken cancellationToken)
        {
            //write the bootstrap runner script to the workspace
            workspace.WriteFile("bootstrapRunner.sh", await BootstrapRunnerScript);

            var scriptName = Path.GetFileName(workspace.BootstrapScriptFilePath);

            var jobName = jobService.BuildJobName(scriptTicket);

            //Deserialize the volume configuration from the environment configuration
            var volumes = KubernetesYaml.Deserialize<List<V1Volume>>(KubernetesConfig.JobVolumeYaml);

            var job = new V1Job
            {
                ApiVersion = "batch/v1",
                Kind = "Job",
                Metadata = new V1ObjectMeta
                {
                    Name = jobName,
                    NamespaceProperty = KubernetesConfig.Namespace,
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
                                    Image = await containerResolver.GetContainerImageForCluster(),
                                    Command = new List<string> { "bash" },
                                    Args = new List<string>
                                    {
                                        $"/data/tentacle-home/{instanceName}/Work/{scriptTicket.TaskId}/bootstrapRunner.sh",
                                        $"/data/tentacle-home/{instanceName}/Work/{scriptTicket.TaskId}",
                                        $"/data/tentacle-home/{instanceName}/Work/{scriptTicket.TaskId}/{scriptName}"
                                    }.Concat((workspace.ScriptArguments ?? Array.Empty<string>())
                                        .SelectMany(arg => new[]
                                        {
                                            $"'{arg}'"
                                        })).ToList(),
                                    VolumeMounts = new List<V1VolumeMount>
                                    {
                                        new("/data/tentacle-home", "tentacle-home"),
                                    },
                                    Env = new List<V1EnvVar>
                                    {
                                        new(EnvironmentVariables.TentacleHome, $"/data/tentacle-home/{instanceName}"),
                                        new(EnvironmentVariables.TentacleJournal, $"/data/tentacle-home/{instanceName}/DeploymentJournal.xml"),
                                        new(EnvironmentVariables.TentacleInstanceName, instanceName),
                                        new(EnvironmentVariables.TentacleVersion, Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleVersion)),
                                        new(EnvironmentVariables.TentacleCertificateSignatureAlgorithm, Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleCertificateSignatureAlgorithm)),
                                        new("OCTOPUS_RUNNING_IN_CONTAINER", "Y")
                                    }
                                }
                            },
                            ServiceAccountName = KubernetesConfig.JobServiceAccountName,
                            RestartPolicy = "Never",
                            Volumes = volumes
                        }
                    },
                    BackoffLimit = 0, //we never want to rerun if it fails
                    TtlSecondsAfterFinished = KubernetesConfig.JobTtlSeconds
                }
            };

            writer.WriteVerbose($"Executing script in Kubernetes Job '{job.Name()}'");

            await jobService.CreateJob(job, cancellationToken);
        }


        void RecordScriptHasStarted(IScriptLogWriter writer)
        {
            try
            {
                var scriptState = stateStore.Load();
                scriptState.Start();
                stateStore.Save(scriptState);
            }
            catch (Exception ex)
            {
                try
                {
                    writer.WriteOutput(ProcessOutputSource.StdOut, $"Warning: An exception occurred saving the ScriptState: {ex.Message}");
                    writer.WriteOutput(ProcessOutputSource.StdOut, ex.ToString());
                }
                catch
                {
                    //we don't care about errors here
                }
            }
        }

        void RecordScriptHasCompleted(int exitCode)
        {
            try
            {
                var scriptState = stateStore.Load();
                scriptState.Complete(exitCode);
                stateStore.Save(scriptState);
            }
            catch (Exception ex)
            {
                try
                {
                    using var writer = ScriptLog.CreateWriter();
                    writer.WriteOutput(ProcessOutputSource.StdOut, $"Warning: An exception occurred saving the ScriptState: {ex.Message}");
                    writer.WriteOutput(ProcessOutputSource.StdOut, ex.ToString());
                }
                catch
                {
                    //we don't care about errors here
                }
            }
        }
    }
}