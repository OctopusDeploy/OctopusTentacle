using k8s;

namespace Octopus.Tentacle.Kubernetes
{
    public sealed class InClusterKubernetesClientConfigProvider : IKubernetesClientConfigProvider
    {
        public KubernetesClientConfiguration Get()
        {
            return KubernetesClientConfiguration.InClusterConfig();
        }
    }
}