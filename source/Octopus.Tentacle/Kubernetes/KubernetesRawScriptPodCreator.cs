using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using k8s.Models;
using Octopus.Diagnostics;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesRawScriptPodCreator : IKubernetesScriptPodCreator
    {
    }

    public class KubernetesRawScriptPodCreator : KubernetesScriptPodCreator, IKubernetesRawScriptPodCreator
    {
        readonly IKubernetesPodContainerResolver containerResolver;

        public KubernetesRawScriptPodCreator(
            IKubernetesPodService podService,
            IKubernetesPodMonitor podMonitor,
            IKubernetesSecretService secretService,
            IKubernetesPodContainerResolver containerResolver,
            IApplicationInstanceSelector appInstanceSelector,
            ISystemLog log,
            ITentacleScriptLogProvider scriptLogProvider,
            IHomeConfiguration homeConfiguration,
            KubernetesPhysicalFileSystem kubernetesPhysicalFileSystem)
            : base(podService, podMonitor, secretService, containerResolver, appInstanceSelector, log, scriptLogProvider, homeConfiguration, kubernetesPhysicalFileSystem)
        {
            this.containerResolver = containerResolver;
        }

        protected override async Task<IList<V1Container>> CreateInitContainers(StartKubernetesScriptCommandV1 command, string podName, string homeDir, string workspacePath)
        {
            var container = new V1Container
            {
                Name = $"{podName}-init",
                Image = command.PodImageConfiguration?.Image ?? await containerResolver.GetContainerImageForCluster(),
                ImagePullPolicy = KubernetesConfig.ScriptPodPullPolicy,
                Command = new List<string> { "sh", "-c", GetInitExecutionScript("/nfs-mount", homeDir, workspacePath) },
                VolumeMounts = new List<V1VolumeMount> { new("/nfs-mount", "init-nfs-volume"), new(homeDir, "tentacle-home") },
                Resources = new V1ResourceRequirements
                {
                    Requests = new Dictionary<string, ResourceQuantity>
                    {
                        ["cpu"] = new("25m"),
                        ["memory"] = new("100Mi")
                    }
                }
            };

            return new List<V1Container> { container };
        }

        protected override async Task<IList<V1Container>> CreateScriptContainers(StartKubernetesScriptCommandV1 command, string podName, string scriptName, string homeDir, string workspacePath, string[]? scriptArguments, InMemoryTentacleScriptLog tentacleScriptLog)
        {
            return new List<V1Container>
            {
                await CreateScriptContainer(command, podName, scriptName, homeDir, workspacePath, scriptArguments, tentacleScriptLog)
            };
        }

        protected override IList<V1Volume> CreateVolumes(StartKubernetesScriptCommandV1 command)
        {
            return new List<V1Volume>
            {
                new()
                {
                    Name = "tentacle-home",
                    EmptyDir = new V1EmptyDirVolumeSource()
                },
                new()
                {
                    Name = "init-nfs-volume",
                    PersistentVolumeClaim = new V1PersistentVolumeClaimVolumeSource
                    {
                        ClaimName = KubernetesConfig.PodVolumeClaimName
                    }
                },
                new()
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
                },
            };
        }

        string GetInitExecutionScript(string nfsVolumeDirectory, string homeDir, string workspacePath)
        {
            var nfsWorkspacePath = Path.Combine(nfsVolumeDirectory, workspacePath);
            var homeWorkspacePath = Path.Combine(homeDir, workspacePath);
            return $@"
                    mkdir -p ""{homeWorkspacePath}"" && cp -r ""{nfsWorkspacePath}""/* ""{homeWorkspacePath}"";
                    ";
        }
    }
}
