using System.Threading.Tasks;
using k8s;
using k8s.Models;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesV1ConfigMapService
    {
        Task<V1ConfigMap> Read(string name, string @namespace);

        Task<V1ConfigMap> Create(V1ConfigMap configMap);

        Task<V1ConfigMap> Replace(V1ConfigMap configMap);
    }

    public class KubernetesV1ConfigMapService : KubernetesService, IKubernetesV1ConfigMapService
    {
        public KubernetesV1ConfigMapService(IKubernetesClientConfigProvider configProvider) : base(configProvider)
        {
        }

        public async Task<V1ConfigMap> Read(string name, string @namespace)
        {
            return await Client.CoreV1.ReadNamespacedConfigMapAsync(name, @namespace);
        }

        public async Task<V1ConfigMap> Create(V1ConfigMap configMap)
        {
            return await Client.CoreV1.CreateNamespacedConfigMapAsync(configMap, configMap.Namespace());
        }

        public async Task<V1ConfigMap> Replace(V1ConfigMap configMap)
        {
            return await Client.CoreV1.ReplaceNamespacedConfigMapAsync(configMap, configMap.Name(), configMap.Namespace());
        }
    }
}