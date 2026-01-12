using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Newtonsoft.Json;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Core.Services.Scripts.Locking;
using Octopus.Tentacle.Kubernetes.Crypto;
using Octopus.Tentacle.Scripts;
using Octopus.Tentacle.Util;
using Octopus.Tentacle.Variables;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesScriptPodCreator
    {
        Task CreatePod(StartKubernetesScriptCommandV1 command, IScriptWorkspace workspace, CancellationToken cancellationToken);
    }

    public class KubernetesScriptPodCreator : IKubernetesScriptPodCreator
    {
        readonly IKubernetesPodService podService;
        readonly IKubernetesPodMonitor podMonitor;
        readonly IKubernetesSecretService secretService;
        readonly IKubernetesPodTemplateService podTemplateService;
        readonly IKubernetesPodContainerResolver containerResolver;
        readonly IApplicationInstanceSelector appInstanceSelector;
        readonly ISystemLog log;
        readonly ITentacleScriptLogProvider scriptLogProvider;
        readonly IHomeConfiguration homeConfiguration;
        readonly KubernetesPhysicalFileSystem kubernetesPhysicalFileSystem;
        readonly IScriptPodLogEncryptionKeyProvider scriptPodLogEncryptionKeyProvider;
        readonly ScriptIsolationMutex scriptIsolationMutex;

        public KubernetesScriptPodCreator(
            IKubernetesPodService podService,
            IKubernetesPodMonitor podMonitor,
            IKubernetesSecretService secretService,
            IKubernetesPodTemplateService podTemplateService,
            IKubernetesPodContainerResolver containerResolver,
            IApplicationInstanceSelector appInstanceSelector,
            ISystemLog log,
            ITentacleScriptLogProvider scriptLogProvider,
            IHomeConfiguration homeConfiguration,
            KubernetesPhysicalFileSystem kubernetesPhysicalFileSystem,
            IScriptPodLogEncryptionKeyProvider scriptPodLogEncryptionKeyProvider,
            ScriptIsolationMutex scriptIsolationMutex)
        {
            this.podService = podService;
            this.podMonitor = podMonitor;
            this.secretService = secretService;
            this.podTemplateService = podTemplateService;
            this.containerResolver = containerResolver;
            this.appInstanceSelector = appInstanceSelector;
            this.log = log;
            this.scriptLogProvider = scriptLogProvider;
            this.homeConfiguration = homeConfiguration;
            this.kubernetesPhysicalFileSystem = kubernetesPhysicalFileSystem;
            this.scriptPodLogEncryptionKeyProvider = scriptPodLogEncryptionKeyProvider;
            this.scriptIsolationMutex = scriptIsolationMutex;
        }

        public async Task CreatePod(StartKubernetesScriptCommandV1 command, IScriptWorkspace workspace, CancellationToken cancellationToken)
        {
            var tentacleScriptLog = scriptLogProvider.GetOrCreate(command.ScriptTicket);

            using (scriptIsolationMutex.Acquire(workspace.IsolationLevel,
                       workspace.ScriptMutexAcquireTimeout,
                       workspace.ScriptMutexName ?? nameof(KubernetesScriptPodCreator),
                       message =>
                       {
                           LogVerboseToBothLogs(message, tentacleScriptLog);
                       },
                       command.TaskId,
                       cancellationToken,
                       log))
            {
                //Write the log encryption key here
                await scriptPodLogEncryptionKeyProvider.GenerateAndWriteEncryptionKeyfileToWorkspace(command.ScriptTicket, cancellationToken);

                //Possibly create the image pull secret name
                var imagePullSecretName = await CreateImagePullSecret(command, cancellationToken);

                //create the k8s pod
                await CreatePod(command, workspace, imagePullSecretName, tentacleScriptLog, cancellationToken);
            }
        }

        async Task<string?> CreateImagePullSecret(StartKubernetesScriptCommandV1 command, CancellationToken cancellationToken)
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

        async Task CreatePod(StartKubernetesScriptCommandV1 command, IScriptWorkspace workspace, string? imagePullSecretName, InMemoryTentacleScriptLog tentacleScriptLog, CancellationToken cancellationToken)
        {
            var homeDir = homeConfiguration.HomeDirectory ?? throw new InvalidOperationException("Home directory is not set.");

            var podName = command.ScriptTicket.ToKubernetesScriptPodName();

            LogVerboseToBothLogs($"Creating Kubernetes Pod '{podName}'.", tentacleScriptLog);

            workspace.CopyFile(KubernetesConfig.BootstrapRunnerExecutablePath, "bootstrapRunner", true);

            var scriptName = Path.GetFileName(workspace.BootstrapScriptFilePath);
            var workspacePath = Path.Combine("Work", workspace.ScriptTicket.TaskId);

            var serviceAccountName = !string.IsNullOrWhiteSpace(command.ScriptPodServiceAccountName)
                ? command.ScriptPodServiceAccountName
                : KubernetesConfig.PodServiceAccountName;

            // image pull secrets may have been defined in the helm chart (e.g. to avoid docker hub rate limiting)
            // we put any specified secret name first so it's resolved first
            var imagePullSecretNames = new[] { imagePullSecretName }
                .Concat(KubernetesConfig.PodImagePullSecretNames)
                .WhereNotNull()
                .Select(secretName => new V1LocalObjectReference(secretName))
                .ToList();

            var scriptPodTemplate = await podTemplateService.GetScriptPodTemplate(cancellationToken);

            var pod = new V1Pod
            {
                Metadata = new V1ObjectMeta
                {
                    Name = podName,
                    NamespaceProperty = KubernetesConfig.Namespace,
                    Labels = Merge(scriptPodTemplate?.PodMetadata?.Labels, GetScriptPodLabels(tentacleScriptLog, command)),
                    Annotations = Merge(scriptPodTemplate?.PodMetadata?.Annotations, GetScriptPodAnnotations(tentacleScriptLog, command))
                },
                //if the script pod template spec has been defined, use that
                Spec = scriptPodTemplate?.PodSpec ?? new V1PodSpec
                {
                    RestartPolicy = "Never",
                    Affinity = ParseScriptPodAffinity(tentacleScriptLog),
                    Tolerations = ParseScriptPodTolerations(tentacleScriptLog),
                    SecurityContext = ParseScriptPodSecurityContext(tentacleScriptLog)
                }
            };

            pod.Spec.InitContainers = await CreateInitContainers(command, podName, homeDir, workspacePath, tentacleScriptLog, scriptPodTemplate?.ScriptInitContainerSpec);
            pod.Spec.Containers = await CreateScriptContainers(command, podName, scriptName, homeDir, workspacePath, workspace.ScriptArguments, tentacleScriptLog, scriptPodTemplate);
            pod.Spec.ImagePullSecrets = imagePullSecretNames;
            pod.Spec.ServiceAccountName = serviceAccountName;
            pod.Spec.Volumes = Merge(pod.Spec.Volumes, CreateVolumes(command));

            var createdPod = await podService.Create(pod, cancellationToken);
            podMonitor.AddPendingPod(command.ScriptTicket, createdPod);

            var scriptContainer = createdPod.Spec.Containers.First(c => c.Name == podName);
            LogVerboseToBothLogs($"Executing script in Kubernetes Pod '{podName}'. Image: '{scriptContainer.Image}'.", tentacleScriptLog);
        }

        protected virtual async Task<IList<V1Container>> CreateScriptContainers(StartKubernetesScriptCommandV1 command, string podName, string scriptName, string homeDir, string workspacePath, string[]? scriptArguments, InMemoryTentacleScriptLog tentacleScriptLog, ScriptPodTemplate? template)
        {
            return new List<V1Container>
            {
                await CreateScriptContainer(command, podName, scriptName, homeDir, workspacePath, scriptArguments, tentacleScriptLog, template?.ScriptContainerSpec)
            }.AddIfNotNull(CreateWatchdogContainer(homeDir, template?.WatchdogContainerSpec));
        }

        protected virtual async Task<IList<V1Container>> CreateInitContainers(StartKubernetesScriptCommandV1 command, string podName, string homeDir, string workspacePath, InMemoryTentacleScriptLog tentacleScriptLog, V1Container? containerSpec)
        {
            await Task.CompletedTask;
            return new List<V1Container>();
        }

        protected virtual IList<V1Volume> CreateVolumes(StartKubernetesScriptCommandV1 command)
        {
            return new List<V1Volume>
            {
                new()
                {
                    Name = "tentacle-home",
                    PersistentVolumeClaim = new V1PersistentVolumeClaimVolumeSource
                    {
                        ClaimName = KubernetesConfig.PodVolumeClaimName
                    }
                },
                CreateAgentUpgradeSecretVolume(),
            };
        }

        protected V1Volume CreateAgentUpgradeSecretVolume()
        {
            return new()
            {
                Name = "agent-upgrade",
                Secret = new V1SecretVolumeSource
                {
                    SecretName = "agent-upgrade-secret",
                    Items = new List<V1KeyToPath>()
                    {
                        new()
                        {
                            Key = ".dockerconfigjson",
                            Path = "config.json"
                        }
                    },
                    Optional = true,
                },
            };
        }

        void LogVerboseToBothLogs(string message, InMemoryTentacleScriptLog tentacleScriptLog)
        {
            log.Verbose(message);
            tentacleScriptLog.Verbose(message);
        }

        protected async Task<V1Container> CreateScriptContainer(StartKubernetesScriptCommandV1 command, string podName, string scriptName, string homeDir, string workspacePath, string[]? scriptArguments, InMemoryTentacleScriptLog tentacleScriptLog, V1Container? containerSpec)
        {
            var spaceInformation = kubernetesPhysicalFileSystem.GetStorageInformation();

            var commandString = string.Join(" ", new[]
                {
                    $"{homeDir}/Work/{command.ScriptTicket.TaskId}/bootstrapRunner",
                    Path.Combine(homeDir, workspacePath),
                    Path.Combine(homeDir, workspacePath, scriptName)
                }.Concat(scriptArguments ?? Array.Empty<string>())
                .Select(x => $"\"{x}\""));

            var envFrom = new List<V1EnvFromSource>();
            //if there is a scrip pod proxies defined
            if (!string.IsNullOrWhiteSpace(KubernetesConfig.ScriptPodProxiesSecretName))
            {
                //add sourcing environment variables from the secret
                envFrom.Add(new V1EnvFromSource
                {
                    SecretRef = new V1SecretEnvSource
                    {
                        Name = KubernetesConfig.ScriptPodProxiesSecretName
                    }
                });
            }

            V1Container container;
            if (containerSpec is not null)
            {
                container = containerSpec;
            }
            else
            {
                var resourceRequirements = GetScriptPodResourceRequirements(tentacleScriptLog);
                container = new V1Container
                {
                    Resources = resourceRequirements
                };
            }

            container.Name = podName;
            container.Image = command.PodImageConfiguration?.Image ?? await containerResolver.GetContainerImageForCluster();
            container.ImagePullPolicy = KubernetesConfig.ScriptPodPullPolicy;
            container.Command = new List<string> { "sh" };
            container.Args = new List<string>
            {
                "-c",
                commandString
            };

            container.VolumeMounts = Merge(container.VolumeMounts, new[]
            {
                new V1VolumeMount(homeDir, "tentacle-home"),
                new V1VolumeMount("/root/agent_upgrade/", "agent-upgrade"),
                new V1VolumeMount("/tmp/agent_upgrade/", "agent-upgrade")
            });

            container.Env = Merge(container.Env, new List<V1EnvVar>
            {
                new(KubernetesConfig.NamespaceVariableName, KubernetesConfig.Namespace),
                new(KubernetesConfig.HelmReleaseNameVariableName, KubernetesConfig.HelmReleaseName),
                new(KubernetesConfig.HelmChartVersionVariableName, KubernetesConfig.HelmChartVersion),
                new(KubernetesConfig.KubernetesMonitorEnabledVariableName, KubernetesConfig.KubernetesMonitorEnabled),
                new(KubernetesConfig.ServerCommsAddressesVariableName, string.Join(",", KubernetesConfig.ServerCommsAddresses)),
                new(KubernetesConfig.PersistentVolumeFreeBytesVariableName, spaceInformation?.freeSpaceBytes.ToString()),
                new(KubernetesConfig.PersistentVolumeSizeBytesVariableName, spaceInformation?.totalSpaceBytes.ToString()),
                new(EnvironmentVariables.TentacleHome, homeDir),
                new(EnvironmentVariables.TentacleInstanceName, appInstanceSelector.Current.InstanceName),
                new(EnvironmentVariables.TentacleVersion, Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleVersion)),
                new(EnvironmentVariables.TentacleCertificateSignatureAlgorithm, Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleCertificateSignatureAlgorithm)),
                new("OCTOPUS_RUNNING_IN_CONTAINER", "Y")

                //We intentionally exclude setting "TentacleJournal" since it doesn't make sense to keep a Deployment Journal for Kubernetes deployments
            });

            container.EnvFrom = Merge(container.EnvFrom, envFrom);
            return container;
        }

        protected V1ResourceRequirements GetScriptPodResourceRequirements(InMemoryTentacleScriptLog tentacleScriptLog)
        {
            var json = KubernetesConfig.PodResourceJson;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    return KubernetesJson.Deserialize<V1ResourceRequirements>(json);
                }
                catch (Exception e)
                {
                    var message = $"Failed to deserialize env.{KubernetesConfig.PodResourceJsonVariableName} into valid pod resource requirements.{Environment.NewLine}JSON value: {json}{Environment.NewLine}Using default resource requests for script pod.";
                    //if we can't parse the JSON, fall back to the defaults below and warn the user
                    log.WarnFormat(e, message);
                    //write a verbose message to the script log.
                    tentacleScriptLog.Verbose(message);
                }
            }

            return new V1ResourceRequirements
            {
                //set resource requests to be quite low for now as the scripts tend to run fairly quickly
                Requests = new Dictionary<string, ResourceQuantity>
                {
                    ["cpu"] = new("25m"),
                    ["memory"] = new("100Mi")
                }
            };
        }

        V1Affinity ParseScriptPodAffinity(InMemoryTentacleScriptLog tentacleScriptLog)
            => ParseScriptPodJson(
                tentacleScriptLog,
                KubernetesConfig.PodAffinityJson,
                KubernetesConfig.PodAffinityJsonVariableName,
                "pod affinity",
                //we default to running on linux/arm64 and linux/amd64 nodes
                new V1Affinity(new V1NodeAffinity(requiredDuringSchedulingIgnoredDuringExecution: new V1NodeSelector(new List<V1NodeSelectorTerm>
                {
                    new(matchExpressions: new List<V1NodeSelectorRequirement>
                    {
                        new("kubernetes.io/os", "In", new List<string> { "linux" }),
                        new("kubernetes.io/arch", "In", new List<string> { "arm64", "amd64" })
                    })
                }))))!;

        List<V1Toleration>? ParseScriptPodTolerations(InMemoryTentacleScriptLog tentacleScriptLog)
            => ParseScriptPodJson<List<V1Toleration>>(
                tentacleScriptLog,
                KubernetesConfig.PodTolerationsJson,
                KubernetesConfig.PodTolerationsJsonVariableName,
                "pod tolerations");

        V1PodSecurityContext? ParseScriptPodSecurityContext(InMemoryTentacleScriptLog tentacleScriptLog)
            => ParseScriptPodJson<V1PodSecurityContext>(
                tentacleScriptLog,
                KubernetesConfig.PodSecurityContextJson,
                KubernetesConfig.PodSecurityContextJsonVariableName,
                "pod security context");

        Dictionary<string, string>? ParseScriptPodAnnotations(InMemoryTentacleScriptLog tentacleScriptLog)
            => ParseScriptPodJson<Dictionary<string, string>>(
                tentacleScriptLog,
                KubernetesConfig.PodAnnotationsJson,
                KubernetesConfig.PodAnnotationsJsonVariableName,
                "pod annotations");

        Dictionary<string, string> GetScriptPodAnnotations(InMemoryTentacleScriptLog tentacleScriptLog, StartKubernetesScriptCommandV1 command)
        {
            var annotations = ParseScriptPodAnnotations(tentacleScriptLog) ?? new Dictionary<string, string>();
            annotations.AddRange(GetAuthContext(command));
            return annotations;
        }

        Dictionary<string, string> GetScriptPodLabels(InMemoryTentacleScriptLog tentacleScriptLog, StartKubernetesScriptCommandV1 command)
        {
            var labels = new Dictionary<string, string>
            {
                ["octopus.com/serverTaskId"] = command.TaskId,
                ["octopus.com/scriptTicketId"] = command.ScriptTicket.TaskId
            };
            var extraLabels = ParseScriptPodJson<Dictionary<string, string>>(
                tentacleScriptLog,
                KubernetesConfig.PodLabelsJson,
                KubernetesConfig.PodLabelsJsonVariableName,
                "pod labels");

            if (extraLabels != null)
            {
                labels.AddRange(extraLabels);
            }

            labels.Add($"{KubernetesConfig.AgentLabelNamespace}/permissions", "enabled");
            labels.AddRange(GetAuthContext(command, true));

            return labels;
        }

        static Dictionary<string, string> GetAuthContext(StartKubernetesScriptCommandV1 command, bool hash = false)
        {
            var dict = new Dictionary<string, string>();

            if (command.AuthContext is null)
            {
                return dict;
            }

            dict[$"{KubernetesConfig.AgentLabelNamespace}/project"] = hash
                ? HashValue(command.AuthContext.ProjectSlug)
                : command.AuthContext.ProjectSlug;

            if (command.AuthContext.ProjectGroupSlug is not null)
            {
                dict[$"{KubernetesConfig.AgentLabelNamespace}/project-group"] = hash
                    ? HashValue(command.AuthContext.ProjectGroupSlug)
                    : command.AuthContext.ProjectGroupSlug;
            }

            dict[$"{KubernetesConfig.AgentLabelNamespace}/environment"] = hash
                ? HashValue(command.AuthContext.EnvironmentSlug)
                : command.AuthContext.EnvironmentSlug;

            if (command.AuthContext.TenantSlug is not null)
            {
                dict[$"{KubernetesConfig.AgentLabelNamespace}/tenant"] = hash
                    ? HashValue(command.AuthContext.TenantSlug)
                    : command.AuthContext.TenantSlug;
            }

            dict[$"{KubernetesConfig.AgentLabelNamespace}/step"] = hash
                ? HashValue(command.AuthContext.StepSlug)
                : command.AuthContext.StepSlug;

            dict[$"{KubernetesConfig.AgentLabelNamespace}/space"] = hash
                ? HashValue(command.AuthContext.SpaceSlug)
                : command.AuthContext.SpaceSlug;

            return dict;
        }

        static string HashValue(string value)
        {
            using var sha1 = SHA1.Create();
            var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(value));
            return BitConverter.ToString(bytes).Replace("-","");
        }

        [return: NotNullIfNotNull("defaultValue")]
        T? ParseScriptPodJson<T>(InMemoryTentacleScriptLog tentacleScriptLog, string? json, string envVarName, string description, T? defaultValue = null) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
                return defaultValue;

            try
            {
                return KubernetesJson.Deserialize<T>(json);
            }
            catch (Exception e)
            {
                var defaultMessage = defaultValue != null ? $"default {description}" : $"no custom {description}";

                var message = $"Failed to deserialize env.{envVarName} into a valid {description}.{Environment.NewLine}JSON value: {json}{Environment.NewLine}Using {defaultMessage} for script pods.";

                //if we can't parse the JSON, fall back to the defaults below and warn the user
                log.WarnFormat(e, message);
                //write a verbose message to the script log.
                tentacleScriptLog.Verbose(message);
            }

            return defaultValue;
        }

        static V1Container? CreateWatchdogContainer(string homeDir, V1Container? containerSpec)
        {
            if (KubernetesConfig.NfsWatchdogImage is null)
            {
                return null;
            }

            V1Container container;
            if (containerSpec is not null)
            {
                container = containerSpec;
            }
            else
            {
                container = new V1Container
                {
                    Resources = new V1ResourceRequirements
                    {
                        //The watchdog should be very lightweight
                        Requests = new Dictionary<string, ResourceQuantity>
                        {
                            ["cpu"] = new("25m"),
                            ["memory"] = new("100Mi")
                        }
                    }
                };
            }

            container.Name = "nfs-watchdog";
            container.Image = KubernetesConfig.NfsWatchdogImage;
            container.VolumeMounts = Merge(container.VolumeMounts, new List<V1VolumeMount>
            {
                new(homeDir, "tentacle-home"),
            });
            container.Env = Merge(container.Env, new[] { new V1EnvVar(EnvironmentVariables.NfsWatchdogDirectory, homeDir) });

            return container;
        }

        protected static IDictionary<string, string> Merge(IDictionary<string, string>? a, IDictionary<string, string>? b)
        {
            var dict = new Dictionary<string, string>();
            if (a is not null)
            {
                dict.AddRange(a);
            }

            if (b is not null)
            {
                dict.AddRange(b);
            }

            return dict;
        }

        protected static IList<T> Merge<T>(IEnumerable<T>? a, IEnumerable<T>? b)
        {
            var list = new List<T>();
            if (a is not null)
            {
                list.AddRange(a);
            }

            if (b is not null)
            {
                list.AddRange(b);
            }

            return list;
        }
    }
}