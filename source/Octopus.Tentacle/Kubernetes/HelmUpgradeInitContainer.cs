using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using k8s.Models;
using Octopus.Client.Model;

namespace Octopus.Tentacle.Kubernetes
{
    public class HelmUpgradeInitContainer
    {
        readonly IKubernetesPodContainerResolver containerResolver;

        public HelmUpgradeInitContainer(IKubernetesPodContainerResolver containerResolver)
        {
            this.containerResolver = containerResolver;
        }

        public async Task<V1Container> Create(string podName, string secretVolumeName, string helmRegistryConfigVolumeName)
        {
            var targetDirectoryName = "/arbitraryLocation";
            var secretMountLocation = "/input";
            
            //need to assume the secret has been mounted already
            var container = new V1Container
            {
                Name = $"{podName}-helm-upgrade-init",
                Image = await containerResolver.GetContainerImageForCluster(),
                ImagePullPolicy = KubernetesConfig.ScriptPodPullPolicy,
                Command = new List<string> { "sh", "-c", GetInitExecutionScript(Path.Combine(secretMountLocation, "config.json"), targetDirectoryName) },
                VolumeMounts = new List<V1VolumeMount> { new(secretMountLocation, secretVolumeName), new(targetDirectoryName, helmRegistryConfigVolumeName) },
                Resources = new V1ResourceRequirements
                {
                    Requests = new Dictionary<string, ResourceQuantity>
                    {
                        ["cpu"] = new("25m"),
                        ["memory"] = new("100Mi")
                    }
                }
            };
            return container;
        }
        
        string GetInitExecutionScript(string sourceFilename, string targetDirectoryName)
        {
            // assume the target volume gets mounted to ~/.config/helm/registry in the execution container
            return $@"
                    cp -r ""{sourceFilename}"" ""{targetDirectoryName}"";
                    ";
        }
    }
}