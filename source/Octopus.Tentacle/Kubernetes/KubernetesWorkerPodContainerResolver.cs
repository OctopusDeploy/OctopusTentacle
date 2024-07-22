using System.Threading.Tasks;

namespace Octopus.Tentacle.Kubernetes
{
    public class KubernetesWorkerPodContainerResolver : IKubernetesPodContainerResolver
    {
        public async Task<string> GetContainerImageForCluster()
        {
            await Task.CompletedTask;
            var tag = KubernetesConfig.ScriptPodContainerImageTag;
            return $"{KubernetesConfig.ScriptPodContainerImage}:{tag}";
        }
    }
}