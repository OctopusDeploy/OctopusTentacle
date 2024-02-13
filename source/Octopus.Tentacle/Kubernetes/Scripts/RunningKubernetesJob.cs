using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Newtonsoft.Json;
using Nito.AsyncEx;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Util;
using Octopus.Tentacle.Variables;

namespace Octopus.Tentacle.Kubernetes.Scripts
{
    public class RunningKubernetesJob : IRunningScript
    {
        static readonly AsyncLazy<string> BootstrapRunnerScript = new(async () =>
        {
            using var stream = typeof(RunningKubernetesJob).Assembly.GetManifestResourceStream("Octopus.Tentacle.Kubernetes.bootstrapRunner.sh");
            using var reader = new StreamReader(stream!, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        });

        readonly IScriptWorkspace workspace;
        readonly ScriptTicket scriptTicket;
        readonly string taskId;
        readonly ILog log;
        readonly IScriptStateStore stateStore;
        readonly IKubernetesJobService jobService;
        readonly IKubernetesSecretService secretService;
        readonly IKubernetesJobContainerResolver containerResolver;
        readonly KubernetesJobScriptExecutionContext executionContext;
        readonly CancellationToken scriptCancellationToken;
        readonly string? instanceName;
        readonly KubernetesJobOutputStreamWriter outputStreamWriter;
        readonly string jobName;

        public int ExitCode { get; private set; }
        public ProcessState State { get; private set; }
        public IScriptLog ScriptLog { get; }

        public RunningKubernetesJob(IScriptWorkspace workspace,
            IScriptLog scriptLog,
            ScriptTicket scriptTicket,
            string taskId,
            ILog log,
            IScriptStateStore stateStore,
            IKubernetesJobService jobService,
            IKubernetesSecretService secretService,
            IKubernetesJobContainerResolver containerResolver,
            IApplicationInstanceSelector appInstanceSelector,
            KubernetesJobScriptExecutionContext executionContext,
            CancellationToken scriptCancellationToken)
        {
            this.workspace = workspace;
            this.scriptTicket = scriptTicket;
            this.taskId = taskId;
            this.log = log;
            this.stateStore = stateStore;
            this.jobService = jobService;
            this.secretService = secretService;
            this.containerResolver = containerResolver;
            this.executionContext = executionContext;
            this.scriptCancellationToken = scriptCancellationToken;
            ScriptLog = scriptLog;
            instanceName = appInstanceSelector.Current.InstanceName;

            outputStreamWriter = new KubernetesJobOutputStreamWriter(workspace);

            // this doesn't change, so build it once
            jobName = jobService.BuildJobName(scriptTicket);
        }

        public async Task Execute()
        {
            var exitCode = -1;

            try
            {
                using var writer = ScriptLog.CreateWriter();

                //register a cancellation callback so that when the script is cancelled, we cancel the job
                //we use a using to make sure this callback is deregistered
                using var cancellationTokenRegistration = scriptCancellationToken.Register(() =>
                {
                    //we spawn the job cancellation on a background thread (as this callback runs synchronously)
                    Task.Run(async () =>
                    {
                        try
                        {
                            WriteVerbose(writer, $"Cancelling Kubernetes Job '{jobName}'.");
                            //first we suspend the job, which terminates the underlying pods
                            await jobService.SuspendJob(scriptTicket, CancellationToken.None);
                            //then we delete the job (because we no longer need it)
                            await jobService.Delete(scriptTicket, CancellationToken.None);
                            WriteVerbose(writer, $"Cancelled Kubernetes Job '{jobName}'.");
                        }
                        catch (Exception e)
                        {
                            WriteError(writer, $"Failed to cancel Kubernetes job {jobName}. {e}");
                        }
                    }, CancellationToken.None);
                });

                try
                {
                    using (ScriptIsolationMutex.Acquire(workspace.IsolationLevel,
                               workspace.ScriptMutexAcquireTimeout,
                               workspace.ScriptMutexName ?? nameof(RunningKubernetesJob),
                               message => writer.WriteOutput(ProcessOutputSource.StdOut, message),
                               taskId,
                               scriptCancellationToken,
                               log))
                    {
                        //Possibly create the image pull secret name
                        var imagePullSecretName = await CreateImagePullSecret(scriptCancellationToken);

                        //create the k8s job
                        await CreateJob(writer, imagePullSecretName, scriptCancellationToken);

                        State = ProcessState.Running;
                        RecordScriptHasStarted(writer);

                        //we now need to monitor the resulting pod status
                        exitCode = await MonitorJobAndLogs(writer);
                    }
                }
                catch (OperationCanceledException)
                {
                    WriteInfo(writer, "Script execution canceled.");
                    exitCode = ScriptExitCodes.CanceledExitCode;
                }
                catch (TimeoutException)
                {
                    WriteInfo(writer, "Script execution timed out.");
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

        async Task<int> MonitorJobAndLogs(IScriptLogWriter writer)
        {
            var jobCompletionCancellationTokenSource = new CancellationTokenSource();
            var checkJobTask = CheckIfJobHasCompleted(jobCompletionCancellationTokenSource);

            //we pass the job completion CTS here because its used to cancel the writing of the job stream
            var monitorJobOutputTask = outputStreamWriter.StreamJobLogsToScriptLog(writer, jobCompletionCancellationTokenSource.Token);

            await Task.WhenAll(checkJobTask, monitorJobOutputTask);

            //once they have both finished, perform one last log read (and don't cancel on it)
            await outputStreamWriter.StreamJobLogsToScriptLog(writer, CancellationToken.None, true);

            //return the exit code of the jobs
            return checkJobTask.Result;
        }

        async Task<int> CheckIfJobHasCompleted(CancellationTokenSource jobCompletionCancellationTokenSource)
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
            }, CancellationToken.None);

            //if the job was killed by cancellation, then we need to change the exit code
            if (scriptCancellationToken.IsCancellationRequested)
            {
                resultStatusCode = ScriptExitCodes.CanceledExitCode;
            }

            jobCompletionCancellationTokenSource.Cancel();

            return resultStatusCode;
        }

        async Task<string?> CreateImagePullSecret(CancellationToken cancellationToken)
        {
            //if we have no feed url or no username, then we can't create image secrets
            if (executionContext.FeedUrl is null || executionContext.FeedUsername is null)
                return null;

            var secretName = CreateImagePullSecretName(executionContext.FeedUrl, executionContext.FeedUsername);

            // this structure is a docker config auth file
            // https://kubernetes.io/docs/tasks/configure-pod-container/pull-image-private-registry/#inspecting-the-secret-regcred
            var config = new Dictionary<string, object>
            {
                ["auths"] = new Dictionary<string, object>
                {
                    [executionContext.FeedUrl] = new
                    {
                        username = executionContext.FeedUsername,
                        password = executionContext.FeedPassword,
                        auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{executionContext.FeedUsername}:{executionContext.FeedPassword}"))
                    }
                }
            };

            var configJson = JsonConvert.SerializeObject(config, Formatting.None);
            var secretData = new Dictionary<string, byte[]>
            {
                [".dockerconfigjson"] = Encoding.UTF8.GetBytes(configJson)
            };

            var existingSecret = await secretService.TryGetSecretAsync(secretName, cancellationToken);
            if (existingSecret is null)
            {
                //if there is no secret, create one
                var secret = new V1Secret
                {
                    Type = "kubernetes.io/dockerconfigjson",
                    Metadata = new V1ObjectMeta
                    {
                        Name = secretName,
                        NamespaceProperty = KubernetesConfig.Namespace
                    },
                    Data = new Dictionary<string, byte[]>
                    {
                        [".dockerconfigjson"] = Encoding.UTF8.GetBytes(configJson)
                    }
                };

                await secretService.CreateSecretAsync(secret, cancellationToken);
            }
            else
            {
                //patch the existing secret with the data (just in case the password has changed for this feed/user combo)
                await secretService.UpdateSecretDataAsync(secretName, secretData, cancellationToken);
            }

            return secretName;
        }

        static string CreateImagePullSecretName(string feedUrl, string? username)
        {
            //The secret name is the domain of the feed & the username in a hash
            //We use SHA1 because we want a small hash (as a secrets name is max 253 chars) and we aren't hashing secure data
            using var sha1 = SHA1.Create();
            var feedUri = new Uri(feedUrl);

            var hash = Convert.ToBase64String(sha1.ComputeHash(Encoding.UTF8.GetBytes($"{feedUri.DnsSafeHost}:{username}")));

            //remove all special chars from the hash
            var sanitizedHash = new string(hash.Where(c => c != '/' && c != '+' && c != '=').ToArray());

            //the secret name must be lowercase. We lose a bit of uniqueness as base64 is case-sensitive
            //but the downside of a clash isn't _that_ bad (just overwriting an existing secret)
            return $"octopus-feed-cred-{sanitizedHash}".ToLowerInvariant();
        }

        async Task CreateJob(IScriptLogWriter writer, string? imagePullSecretName, CancellationToken cancellationToken)
        {
            WriteVerbose(writer, $"Creating Kubernetes Job '{jobName}'.");

            //write the bootstrap runner script to the workspace
            workspace.WriteFile("bootstrapRunner.sh", await BootstrapRunnerScript.Task);

            var scriptName = Path.GetFileName(workspace.BootstrapScriptFilePath);

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
                                    Image = executionContext.Image ?? await containerResolver.GetContainerImageForCluster(),
                                    Command = new List<string> { "bash" },
                                    Args = new List<string>
                                        {
                                            $"/octopus/Work/{scriptTicket.TaskId}/bootstrapRunner.sh",
                                            $"/octopus/Work/{scriptTicket.TaskId}",
                                            $"/octopus/Work/{scriptTicket.TaskId}/{scriptName}"
                                        }.Concat(workspace.ScriptArguments ?? Array.Empty<string>())
                                        .ToList(),
                                    VolumeMounts = new List<V1VolumeMount>
                                    {
                                        new("/octopus", "tentacle-home"),
                                    },
                                    Env = new List<V1EnvVar>
                                    {
                                        new(EnvironmentVariables.TentacleHome, $"/octopus"),
                                        new(EnvironmentVariables.TentacleInstanceName, instanceName),
                                        new(EnvironmentVariables.TentacleVersion, Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleVersion)),
                                        new(EnvironmentVariables.TentacleCertificateSignatureAlgorithm, Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleCertificateSignatureAlgorithm)),
                                        new("OCTOPUS_RUNNING_IN_CONTAINER", "Y")

                                        //We intentionally exclude setting "TentacleJournal" since it doesn't make sense to keep a Deployment Journal for Kubernetes deployments
                                    },
                                    Resources = new V1ResourceRequirements
                                    {
                                        //set resource requests to be quite low for now as the jobs tend to run fairly quickly
                                        Requests = new Dictionary<string, ResourceQuantity>
                                        {
                                            ["cpu"] = new("25m"),
                                            ["memory"] = new ("100Mi")
                                        }
                                    }
                                }
                            },
                            //only include the image pull secret name if it's actually been defined
                            ImagePullSecrets = imagePullSecretName is not null
                                ? new List<V1LocalObjectReference>
                                {
                                    new(imagePullSecretName)
                                }
                                : new List<V1LocalObjectReference>(),
                            ServiceAccountName = KubernetesConfig.JobServiceAccountName,
                            RestartPolicy = "Never",
                            Volumes = volumes,
                            //currently we only support running on linux nodes
                            NodeSelector = new Dictionary<string, string>
                            {
                                ["kubernetes.io/os"] = "linux"
                            }
                        }
                    },
                    BackoffLimit = 0, //we never want to rerun if it fails
                    TtlSecondsAfterFinished = KubernetesConfig.JobTtlSeconds
                }
            };

            await jobService.CreateJob(job, cancellationToken);

            WriteVerbose(writer, $"Executing script in Kubernetes Job '{jobName}'.");
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
                    WriteInfo(writer, $"Warning: An exception occurred saving the ScriptState for job '{jobName}': {ex.Message}");
                    WriteInfo(writer, ex.ToString());
                }
                catch
                {
                    //we don't care about errors here
                }
            }
        }

        void RecordScriptHasCompleted(int exitCode)
        {
            using var writer = ScriptLog.CreateWriter();
            try
            {
                var scriptState = stateStore.Load();
                scriptState.Complete(exitCode);
                stateStore.Save(scriptState);
                WriteVerbose(writer, $"Kubernetes Job '{jobName}' completed with exit code {exitCode}");
            }
            catch (Exception ex)
            {
                try
                {
                    WriteInfo(writer, $"Warning: An exception occurred saving the ScriptState for job '{jobName}': {ex.Message}");
                    WriteInfo(writer, ex.ToString());
                }
                catch
                {
                    //we don't care about errors here
                }
            }
        }

        void WriteInfo(IScriptLogWriter writer, string message)
        {
            writer.WriteOutput(ProcessOutputSource.StdOut, message);
            log.Info(message);
        }

        void WriteError(IScriptLogWriter writer, string message)
        {
            writer.WriteOutput(ProcessOutputSource.StdErr, message);
            log.Error(message);
        }

        void WriteVerbose(IScriptLogWriter writer, string message)
        {
            writer.WriteVerbose(message);
            log.Verbose(message);
        }
    }
}
