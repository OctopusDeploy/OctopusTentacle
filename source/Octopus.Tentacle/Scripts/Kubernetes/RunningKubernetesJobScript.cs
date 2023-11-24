using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using k8s.Models;
using Nito.AsyncEx;
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
        readonly IScriptStateStore stateStore;
        readonly IKubernetesJobService jobService;
        readonly IKubernetesClusterService kubernetesClusterService;
        readonly KubernetesJobScriptExecutionContext executionContext;
        readonly CancellationToken scriptCancellationToken;
        readonly string? instanceName;
        readonly AsyncLazy<string> bootstrapRunnerScript;

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
            IKubernetesClusterService kubernetesClusterService,
            IApplicationInstanceSelector appInstanceSelector,
            KubernetesJobScriptExecutionContext executionContext)
        {
            this.workspace = workspace;
            this.scriptTicket = scriptTicket;
            this.taskId = taskId;
            this.log = log;
            this.stateStore = stateStore;
            this.jobService = jobService;
            this.kubernetesClusterService = kubernetesClusterService;
            this.executionContext = executionContext;
            this.scriptCancellationToken = scriptCancellationToken;
            ScriptLog = scriptLog;
            instanceName = appInstanceSelector.Current.InstanceName;

            bootstrapRunnerScript = new AsyncLazy<string>(async () =>
            {
                using var stream = GetType().Assembly.GetManifestResourceStream("Octopus.Tentacle.Kubernetes.bootstrapRunner.sh");
                using var reader = new StreamReader(stream!, Encoding.UTF8);
                return await reader.ReadToEndAsync();
            });
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

            //we pass the job completion CTS here because
            var monitorJobOutputTask = ReadJobOutputStreams(writer, jobCompletionCancellationTokenSource.Token);

            await Task.WhenAll(checkJobTask, monitorJobOutputTask);

            //once they have both finished, perform one last log read
            await ReadJobOutputStreams(writer, cancellationToken);

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

        async Task ReadJobOutputStreams(IScriptLogWriter writer, CancellationToken cancellationToken)
        {
            //open the file streams for reading
            using var stdOutStream = new StreamReader(workspace.OpenFileStreamForReading("stdout.log"), Encoding.UTF8);
            using var stdErrStream = new StreamReader(workspace.OpenFileStreamForReading("stderr.log"), Encoding.UTF8);

            long lastStdOutOffset = 0;
            long lastStdErrOffset = 0;

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                //Read both the stdout and stderr log files
                var stdOutReadTask = ReadLogFileTail(writer, stdOutStream, ProcessOutputSource.StdOut, lastStdOutOffset);
                var stdErrReadTask = ReadLogFileTail(writer, stdErrStream, ProcessOutputSource.StdErr, lastStdErrOffset);

                //wait for them to both complete
                await Task.WhenAll(stdOutReadTask, stdErrReadTask);

                //store the final offsets
                lastStdOutOffset = stdOutReadTask.Result.FinalOffset;
                lastStdErrOffset = stdErrReadTask.Result.FinalOffset;

                //stitch the log lines together and order by occurred, then write to actual log
                var orderedLogLines = stdOutReadTask.Result.Logs
                    .Concat(stdErrReadTask.Result.Logs)
                    .OrderBy(ll => ll.Occurred);

                //write all the read log lines to the output script log
                foreach (var logLine in orderedLogLines)
                {
                    writer.WriteOutput(logLine.Source, logLine.Message, logLine.Occurred);
                }

                //wait for 250ms before reading the logs again
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
        }

        record LogLine(ProcessOutputSource Source, string Message, DateTimeOffset Occurred);

        static async Task<(IEnumerable<LogLine> Logs, long FinalOffset)> ReadLogFileTail(IScriptLogWriter writer, StreamReader reader, ProcessOutputSource source, long lastOffset)
        {
            if (reader.BaseStream.Length == lastOffset)
                return (Enumerable.Empty<LogLine>(), lastOffset);

            reader.BaseStream.Seek(lastOffset, SeekOrigin.Begin);

            var newLines = new List<LogLine>();
            string? line;
            do
            {
                line = await reader.ReadLineAsync();
                if (line.IsNullOrEmpty())
                    break;

                var logParts = line!.Split(new[] { '|' }, 2);

                if (logParts.Length != 2)
                {
                    writer.WriteOutput(ProcessOutputSource.StdErr, $"Invalid log line detected. '{line}' is not correctly pipe-delimited.");
                    continue;
                }

                //part 1 is the datetimeoffset
                if (!DateTimeOffset.TryParse(logParts[0], out var occurred))
                {
                    writer.WriteOutput(ProcessOutputSource.StdErr, $"Failed to parse '{logParts[0]}' as a DateTimeOffset. Using DateTimeOffset.UtcNow.");
                    occurred = DateTimeOffset.UtcNow;
                    ;
                }

                //add the new line
                newLines.Add(new LogLine(source, logParts[1], occurred));
            } while (!line.IsNullOrEmpty());

            return (newLines, reader.BaseStream.Position);
        }

        async Task CreateJob(IScriptLogWriter writer, CancellationToken cancellationToken)
        {
            //write the bootstrap runner script to the workspace
            workspace.WriteFile("bootstrapRunner.sh", await bootstrapRunnerScript);

            var scriptName = Path.GetFileName(workspace.BootstrapScriptFilePath);

            var jobName = jobService.BuildJobName(scriptTicket);

            //Deserialize the volume configuration
            var volumes = k8s.KubernetesYaml.Deserialize<List<V1Volume>>(KubernetesConfig.JobVolumeYaml);
            
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
                                    Image = executionContext.ContainerImage ?? await GetDefaultContainer(),
                                    //do dark bash business #bash-wizards
                                    Command = new List<string> { "bash" },
                                    Args = new List<string>
                                    {
                                        $"'/data/tentacle-home/{instanceName}/Work/{scriptTicket.TaskId}/bootstrapRunner.sh'",
                                        $"'/data/tentacle-home/{instanceName}/Work/{scriptTicket.TaskId}'",
                                        $"'/data/tentacle-home/{instanceName}/Work/{scriptTicket.TaskId}/{scriptName}'"
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
                                        new(EnvironmentVariables.TentacleVersion, Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleVersion)),
                                        new(EnvironmentVariables.TentacleCertificateSignatureAlgorithm, Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleCertificateSignatureAlgorithm)),
                                        new("OCTOPUS_RUNNING_IN_CONTAINER", "Y")
                                    }
                                }
                            },
                            ServiceAccountName = KubernetesConfig.ServiceAccountName,
                            RestartPolicy = "Never",
                            Volumes = volumes
                            // new List<V1Volume>
                            // {
                            //     new()
                            //     {
                            //         Name = "tentacle-home",
                            //         PersistentVolumeClaim = new V1PersistentVolumeClaimVolumeSource("tentacle-home-pv-claim")
                            //     }
                            // }
                        }
                    },
                    BackoffLimit = 0, //we never want to rerun if it fails
                    TtlSecondsAfterFinished = 1800 //30min
                }
            };

            writer.WriteVerbose($"Executing script in Kubernetes Job '{job.Name()}'");

            await jobService.CreateJob(job, cancellationToken);
        }

        static readonly List<Version> KnownLatestContainerTags = new List<Version>
        {
            new(1, 26, 3),
            new(1, 27, 3),
            new(1, 28, 2),
        };

        async Task<string> GetDefaultContainer()
        {
            var clusterVersion = await kubernetesClusterService.GetClusterVersion();

            //find the highest tag for this cluster version
            var tagVersion = KnownLatestContainerTags.FirstOrDefault(tag => tag.Major == clusterVersion.Major && tag.Minor == clusterVersion.Minor);

            var tag = tagVersion?.ToString(3) ?? "latest";

            return $"octopuslabs/k8s-workertools:{tag}";
            //return "octopusdeploy/worker-tools:ubuntu.22.04";
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