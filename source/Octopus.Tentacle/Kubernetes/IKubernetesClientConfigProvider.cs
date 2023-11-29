using k8s;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesClientConfigProvider
    {
        KubernetesClientConfiguration Get();
    }
}