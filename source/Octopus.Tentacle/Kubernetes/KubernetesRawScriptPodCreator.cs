using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using k8s.Models;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Contracts.KubernetesScriptServiceV1;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Core.Services.Scripts.Locking;
using Octopus.Tentacle.Kubernetes.Crypto;

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
            IKubernetesCustomResourceService customResourceService,
            IKubernetesPodContainerResolver containerResolver,
            IApplicationInstanceSelector appInstanceSelector,
            ISystemLog log,
            ITentacleScriptLogProvider scriptLogProvider,
            IHomeConfiguration homeConfiguration,
            KubernetesPhysicalFileSystem kubernetesPhysicalFileSystem,
            IScriptPodLogEncryptionKeyProvider scriptPodLogEncryptionKeyProvider,
            ScriptIsolationMutex scriptIsolationMutex)
            : base(podService, podMonitor, secretService, customResourceService, containerResolver, appInstanceSelector, log, scriptLogProvider, homeConfiguration, kubernetesPhysicalFileSystem, scriptPodLogEncryptionKeyProvider, scriptIsolationMutex)
        {
            this.containerResolver = containerResolver;
        }

        protected override async Task<IList<V1Container>> CreateInitContainers(StartKubernetesScriptCommandV1 command, string podName, string homeDir, string workspacePath, InMemoryTentacleScriptLog tentacleScriptLog, V1Container? containerSpec)
        {
            // Deep clone the container spec to avoid modifying the original
            var container = containerSpec.Clone() ??
                new V1Container
                {
                    Resources = GetScriptPodResourceRequirements(tentacleScriptLog)
                };

            container.Name = $"{podName}-init";
            container.Image = command.PodImageConfiguration?.Image ?? await containerResolver.GetContainerImageForCluster();
            container.ImagePullPolicy = KubernetesConfig.ScriptPodPullPolicy;
            container.Command = new List<string> { "sh", "-c", GetInitExecutionScript("/nfs-mount", homeDir, workspacePath) };
            container.VolumeMounts = Merge(container.VolumeMounts, new[] { new V1VolumeMount("/nfs-mount", "init-nfs-volume"), new V1VolumeMount(homeDir, "tentacle-home") });

            return new List<V1Container> { container };
        }

        protected override async Task<IList<V1Container>> CreateScriptContainers(StartKubernetesScriptCommandV1 command, string podName, string scriptName, string homeDir, string workspacePath, string[]? scriptArguments, InMemoryTentacleScriptLog tentacleScriptLog, ScriptPodTemplateSpec? spec)
        {
            return new List<V1Container>
            {
                await CreateScriptContainer(command, podName, scriptName, homeDir, workspacePath, scriptArguments, tentacleScriptLog, spec?.ScriptContainerSpec)
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
                CreateAgentUpgradeSecretVolume(),
            };
        }

        static string GetInitExecutionScript(string nfsVolumeDirectory, string homeDir, string workspacePath)
        {
            var nfsWorkspacePath = Path.Combine(nfsVolumeDirectory, workspacePath);
            var homeWorkspacePath = Path.Combine(homeDir, workspacePath);
            return $@"
                    mkdir -p ""{homeWorkspacePath}"" && cp -r ""{nfsWorkspacePath}""/* ""{homeWorkspacePath}"";
                    ";
        }
    }
}