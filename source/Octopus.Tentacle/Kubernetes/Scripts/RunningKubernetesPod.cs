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
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1Alpha;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Util;
using Octopus.Tentacle.Variables;

namespace Octopus.Tentacle.Kubernetes.Scripts
{
    public class RunningKubernetesPod : IRunningScript
    {
        public delegate RunningKubernetesPod Factory(
            IScriptWorkspace workspace,
            IScriptLog scriptLog,
            StartKubernetesScriptCommandV1Alpha command,
            IScriptStateStore stateStore,
            CancellationToken scriptCancellationToken);

        readonly IScriptWorkspace workspace;
        readonly StartKubernetesScriptCommandV1Alpha command;
        readonly ScriptTicket scriptTicket;
        readonly string taskId;
        readonly ILog log;
        readonly IScriptStateStore stateStore;
        readonly IKubernetesPodService podService;
        readonly IKubernetesPodStatusProvider podStatusProvider;
        readonly IKubernetesSecretService secretService;
        readonly IKubernetesPodContainerResolver containerResolver;
        readonly CancellationToken scriptCancellationToken;
        readonly string? instanceName;
        readonly KubernetesPodOutputStreamWriter outputStreamWriter;
        readonly string podName;

        public int ExitCode { get; private set; }
        public ProcessState State { get; private set; }
        public IScriptLog ScriptLog { get; }

        public RunningKubernetesPod(IScriptWorkspace workspace,
            IScriptLog scriptLog,
            StartKubernetesScriptCommandV1Alpha command,
            IScriptStateStore stateStore,
            CancellationToken scriptCancellationToken,
            ISystemLog log,
            IKubernetesPodService podService,
            IKubernetesPodStatusProvider podStatusProvider,
            IKubernetesSecretService secretService,
            IKubernetesPodContainerResolver containerResolver,
            IApplicationInstanceSelector appInstanceSelector)
        {
            this.workspace = workspace;
            this.command = command;
            scriptTicket = command.ScriptTicket;
            taskId = command.TaskId;
            this.log = log;
            this.stateStore = stateStore;
            this.podService = podService;
            this.podStatusProvider = podStatusProvider;
            this.secretService = secretService;
            this.containerResolver = containerResolver;
            this.scriptCancellationToken = scriptCancellationToken;
            ScriptLog = scriptLog;
            instanceName = appInstanceSelector.Current.InstanceName;

            outputStreamWriter = new KubernetesPodOutputStreamWriter(workspace);

            State = ProcessState.Pending;

            // this doesn't change, so build it once
            podName = scriptTicket.ToKubernetesScriptPobName();
        }

        public async Task Execute()
        {
            var exitCode = -1;

            try
            {
                using var writer = ScriptLog.CreateWriter();

                //register a cancellation callback so that when the script is cancelled, we cancel the pod
                //we use a using to make sure this callback is deregistered
                using var cancellationTokenRegistration = scriptCancellationToken.Register(() =>
                {
                    //we spawn the pod cancellation on a background thread (as this callback runs synchronously)
                    Task.Run(async () =>
                    {
                        try
                        {
                            WriteVerbose(writer, $"Deleting Kubernetes Pod '{podName}'.");
                            //We delete the pob (because we no longer need it)
                            await podService.Delete(scriptTicket, CancellationToken.None);
                            WriteVerbose(writer, $"Deleted Kubernetes Pod '{podName}'.");
                        }
                        catch (Exception e)
                        {
                            WriteError(writer, $"Failed to delete Kubernetes Pod {podName}. {e}");
                        }
                    }, CancellationToken.None);
                });

                try
                {
                    using (ScriptIsolationMutex.Acquire(workspace.IsolationLevel,
                               workspace.ScriptMutexAcquireTimeout,
                               workspace.ScriptMutexName ?? nameof(RunningKubernetesPod),
                               message => writer.WriteOutput(ProcessOutputSource.StdOut, message),
                               taskId,
                               scriptCancellationToken,
                               log))
                    {
                        //Possibly create the image pull secret name
                        var imagePullSecretName = await CreateImagePullSecret(scriptCancellationToken);

                        //create the k8s pod
                        await CreatePod(writer, imagePullSecretName, scriptCancellationToken);

                        State = ProcessState.Running;
                        RecordScriptHasStarted(writer);

                        //we now need to monitor the resulting pod status
                        exitCode = await MonitorPodAndLogs(writer);
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
            catch (Exception ex)
            {
                log.Error(ex, $"Failed to execute script {scriptTicket.TaskId} in pod {podName}");
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

        async Task<int> MonitorPodAndLogs(IScriptLogWriter writer)
        {
            var podCompletionCancellationTokenSource = new CancellationTokenSource();
            var checkPodTask = CheckIfPodHasCompleted(podCompletionCancellationTokenSource);

            //we pass the pod completion CTS here because its used to cancel the writing of the pod stream
            var monitorPodOutputTask = outputStreamWriter.StreamPodLogsToScriptLog(writer, podCompletionCancellationTokenSource.Token);

            await Task.WhenAll(checkPodTask, monitorPodOutputTask);

            //once they have both finished, perform one last log read (and don't cancel on it)
            await outputStreamWriter.StreamPodLogsToScriptLog(writer, CancellationToken.None, true);

            //return the exit code of the pod
            return checkPodTask.Result;
        }

        async Task<int> CheckIfPodHasCompleted(CancellationTokenSource podCompletionCancellationTokenSource)
        {
            var resultStatusCode = ScriptExitCodes.UnknownScriptExitCode;
            ITrackedScriptPod? status = null;
            while (!scriptCancellationToken.IsCancellationRequested)
            {
                status = podStatusProvider.TryGetTrackedScriptPod(scriptTicket);
                if (status is not null && status.State == TrackedScriptPodState.Succeeded)
                {
                    resultStatusCode = 0;
                    break;
                }

                if (status is not null && status.State == TrackedScriptPodState.Failed)
                {
                    resultStatusCode = status.ExitCode!.Value;
                    break;
                }

                await Task.Delay(250, scriptCancellationToken);
            }

            //if the pod was killed by cancellation, then we need to change the exit code
            if (scriptCancellationToken.IsCancellationRequested)
            {
                resultStatusCode = ScriptExitCodes.CanceledExitCode;
            }

            podCompletionCancellationTokenSource.Cancel();

            log.Verbose($"Pod {podName} completed.{status}");

            return resultStatusCode;
        }

        async Task<string?> CreateImagePullSecret(CancellationToken cancellationToken)
        {
            //if we have no feed url or no username, then we can't create image secrets
            if (command.PodImageConfiguration?.FeedUrl is null || command.PodImageConfiguration?.FeedUsername is null)
                return null;

            var secretName = CreateImagePullSecretName(command.PodImageConfiguration.FeedUrl, command.PodImageConfiguration.FeedUsername);

            // this structure is a docker config auth file
            // https://kubernetes.io/docs/tasks/configure-pod-container/pull-image-private-registry/#inspecting-the-secret-regcred
            var config = new Dictionary<string, object>
            {
                ["auths"] = new Dictionary<string, object>
                {
                    [command.PodImageConfiguration.FeedUrl] = new
                    {
                        username = command.PodImageConfiguration.FeedUsername,
                        password = command.PodImageConfiguration.FeedPassword,
                        auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{command.PodImageConfiguration.FeedUsername}:{command.PodImageConfiguration.FeedPassword}"))
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

        async Task CreatePod(IScriptLogWriter writer, string? imagePullSecretName, CancellationToken cancellationToken)
        {
            WriteVerbose(writer, $"Creating Kubernetes Pod '{podName}'.");

            //write the bootstrap runner script to the workspace
            workspace.CopyFile(KubernetesConfig.BootstrapRunnerExecutablePath, "bootstrapRunner");

            var scriptName = Path.GetFileName(workspace.BootstrapScriptFilePath);

            //Deserialize the volume configuration from the environment configuration
            var volumes = KubernetesJson.Deserialize<List<V1Volume>>(KubernetesConfig.PodVolumeJson);

            var pod = new V1Pod
            {
                Metadata = new V1ObjectMeta
                {
                    Name = podName,
                    NamespaceProperty = KubernetesConfig.Namespace,
                    Labels = new Dictionary<string, string>
                    {
                        ["octopus.com/serverTaskId"] = taskId,
                        ["octopus.com/scriptTicketId"] = scriptTicket.TaskId
                    }
                },
                Spec = new V1PodSpec
                {
                    Containers = new List<V1Container>
                    {
                        new()
                        {
                            Name = podName,
                            Image = command.PodImageConfiguration?.Image ?? await containerResolver.GetContainerImageForCluster(),
                            Command = new List<string> { $"/octopus/Work/{scriptTicket.TaskId}/bootstrapRunner" },
                            Args = new List<string>
                                {
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
                                new(KubernetesConfig.NamespaceVariableName, KubernetesConfig.Namespace),
                                new(KubernetesConfig.HelmReleaseNameVariableName, KubernetesConfig.HelmReleaseName),
                                new(KubernetesConfig.HelmChartVersionVariableName, KubernetesConfig.HelmChartVersion),
                                new(EnvironmentVariables.TentacleHome, $"/octopus"),
                                new(EnvironmentVariables.TentacleInstanceName, instanceName),
                                new(EnvironmentVariables.TentacleVersion, Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleVersion)),
                                new(EnvironmentVariables.TentacleCertificateSignatureAlgorithm, Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleCertificateSignatureAlgorithm)),
                                new("OCTOPUS_RUNNING_IN_CONTAINER", "Y")

                                //We intentionally exclude setting "TentacleJournal" since it doesn't make sense to keep a Deployment Journal for Kubernetes deployments
                            },
                            Resources = new V1ResourceRequirements
                            {
                                //set resource requests to be quite low for now as the scripts tend to run fairly quickly
                                Requests = new Dictionary<string, ResourceQuantity>
                                {
                                    ["cpu"] = new("25m"),
                                    ["memory"] = new("100Mi")
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
                    ServiceAccountName = KubernetesConfig.PodServiceAccountName,
                    RestartPolicy = "Never",
                    Volumes = volumes,
                    //currently we only support running on linux nodes
                    NodeSelector = new Dictionary<string, string>
                    {
                        ["kubernetes.io/os"] = "linux"
                    }
                }
            };

            await podService.Create(pod, cancellationToken);

            WriteVerbose(writer, $"Executing script in Kubernetes Pod '{podName}'.");
        }

        public async Task Cleanup(CancellationToken cancellationToken)
        {
            if (KubernetesConfig.DisableAutomaticPodCleanup)
            {
                log.Verbose($"Not deleting completed pod {podName} as automatic cleanup is disabled.");
                return;
            }

            try
            {
                log.Verbose($"Deleting completed pod {podName}.");
                await podService.Delete(scriptTicket, cancellationToken);
            }
            catch (Exception ex)
            {
                //we can't write this back to the script log as it's already cleaned up at this point
                log.Error(ex, $"Failed to delete Pod {podName}.");
            }
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
                    WriteInfo(writer, $"Warning: An exception occurred saving the ScriptState for pod '{podName}': {ex.Message}");
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
                WriteVerbose(writer, $"Kubernetes Pod '{podName}' completed with exit code {exitCode}");
            }
            catch (Exception ex)
            {
                try
                {
                    WriteInfo(writer, $"Warning: An exception occurred saving the ScriptState for pod '{podName}': {ex.Message}");
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