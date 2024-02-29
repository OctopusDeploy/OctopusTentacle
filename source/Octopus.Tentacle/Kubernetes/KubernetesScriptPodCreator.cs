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
using Octopus.Tentacle.Contracts.ScriptServiceV3Alpha;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Variables;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesScriptPodCreator
    {
        Task CreatePod(StartScriptCommandV3Alpha command, IScriptWorkspace workspace, CancellationToken cancellationToken);
    }

    public class KubernetesScriptPodCreator : IKubernetesScriptPodCreator
    {
        static readonly AsyncLazy<string> BootstrapRunnerScript = new(async () =>
        {
            using var stream = typeof(KubernetesScriptPodCreator).Assembly.GetManifestResourceStream("Octopus.Tentacle.Kubernetes.bootstrapRunner.sh");
            using var reader = new StreamReader(stream!, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        });

        readonly IKubernetesPodService podService;
        readonly IKubernetesSecretService secretService;
        readonly IKubernetesPodContainerResolver containerResolver;
        readonly IApplicationInstanceSelector appInstanceSelector;
        readonly ISystemLog log;

        public KubernetesScriptPodCreator(
            IKubernetesPodService podService,
            IKubernetesSecretService secretService,
            IKubernetesPodContainerResolver containerResolver,
            IApplicationInstanceSelector appInstanceSelector,
            ISystemLog log)
        {
            this.podService = podService;
            this.secretService = secretService;
            this.containerResolver = containerResolver;
            this.appInstanceSelector = appInstanceSelector;
            this.log = log;
        }

        public async Task CreatePod(StartScriptCommandV3Alpha command, IScriptWorkspace workspace, CancellationToken cancellationToken)
        {
            using (ScriptIsolationMutex.Acquire(workspace.IsolationLevel,
                       workspace.ScriptMutexAcquireTimeout,
                       workspace.ScriptMutexName ?? nameof(KubernetesScriptPodCreator),
                       message => { },
                       command.TaskId,
                       cancellationToken,
                       log))
            {
                if (command.ExecutionContext is not KubernetesAgentScriptExecutionContext agentExecutionContext)
                    throw new InvalidOperationException("Bad stuffs bad");

                //Possibly create the image pull secret name
                var imagePullSecretName = await CreateImagePullSecret(agentExecutionContext, cancellationToken);

                //create the k8s pod
                await CreatePod(command, agentExecutionContext, workspace, imagePullSecretName, cancellationToken);
            }
        }

                async Task<string?> CreateImagePullSecret(KubernetesAgentScriptExecutionContext agentExecutionContext, CancellationToken cancellationToken)
        {
            //if we have no feed url or no username, then we can't create image secrets
            if (agentExecutionContext.FeedUrl is null || agentExecutionContext.FeedUsername is null)
                return null;

            var secretName = CreateImagePullSecretName(agentExecutionContext.FeedUrl, agentExecutionContext.FeedUsername);

            // this structure is a docker config auth file
            // https://kubernetes.io/docs/tasks/configure-pod-container/pull-image-private-registry/#inspecting-the-secret-regcred
            var config = new Dictionary<string, object>
            {
                ["auths"] = new Dictionary<string, object>
                {
                    [agentExecutionContext.FeedUrl] = new
                    {
                        username = agentExecutionContext.FeedUsername,
                        password = agentExecutionContext.FeedPassword,
                        auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{agentExecutionContext.FeedUsername}:{agentExecutionContext.FeedPassword}"))
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

        async Task CreatePod(StartScriptCommandV3Alpha command, KubernetesAgentScriptExecutionContext agentExecutionContext, IScriptWorkspace workspace, string? imagePullSecretName, CancellationToken cancellationToken)
        {
            var podName = command.ScriptTicket.ToKubernetesScriptPobName();

            log.Verbose( $"Creating Kubernetes Pod '{podName}'.");

            //write the bootstrap runner script to the workspace
            workspace.WriteFile("bootstrapRunner.sh", await BootstrapRunnerScript.Task);

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
                        ["octopus.com/serverTaskId"] = command.TaskId,
                        ["octopus.com/scriptTicketId"] = command.ScriptTicket.TaskId
                    }
                },
                Spec = new V1PodSpec
                {
                    Containers = new List<V1Container>
                    {
                        new()
                        {
                            Name = podName,
                            Image = agentExecutionContext.Image ?? await containerResolver.GetContainerImageForCluster(),
                            Command = new List<string> { "bash" },
                            Args = new List<string>
                                {
                                    $"/octopus/Work/{command.ScriptTicket.TaskId}/bootstrapRunner.sh",
                                    $"/octopus/Work/{command.ScriptTicket.TaskId}/{scriptName}"
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
                                new(EnvironmentVariables.TentacleInstanceName, appInstanceSelector.Current.InstanceName),
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

            log.Verbose($"Executing script in Kubernetes Pod '{podName}'.");
        }


    }
}