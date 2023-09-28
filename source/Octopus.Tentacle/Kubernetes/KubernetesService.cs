using k8sClient = k8s.Kubernetes;

namespace Octopus.Tentacle.Kubernetes
{
    public abstract class KubernetesService
    {
        protected k8sClient Client { get; }

        protected KubernetesService(IKubernetesClientConfigProvider configProvider)
        {
            Client = new k8sClient(configProvider.Get());
        }
    }
}