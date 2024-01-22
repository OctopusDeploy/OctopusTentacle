using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Autorest;
using k8s.Models;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesConfigMapService
    {
        Task<V1ConfigMap?> TryGet(string name, CancellationToken cancellationToken);

        Task<V1ConfigMap> Patch(string name, IDictionary<string, string> data, CancellationToken cancellationToken);
    }

    public class KubernetesConfigMapService : KubernetesService, IKubernetesConfigMapService
    {
        public KubernetesConfigMapService(IKubernetesClientConfigProvider configProvider) : base(configProvider)
        {
        }

        public async Task<V1ConfigMap?> TryGet(string name, CancellationToken cancellationToken)
        {
            try
            {
                return await Client.CoreV1.ReadNamespacedConfigMapAsync(name, KubernetesConfig.Namespace, cancellationToken: cancellationToken);
            }
            catch (HttpOperationException opException)
                when (opException.Response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task<V1ConfigMap> Patch(string name, IDictionary<string, string> data, CancellationToken cancellationToken)
        {
            var configMap = new V1ConfigMap
            {
                Data = data
            };

            var configMapJson = KubernetesJson.Serialize(configMap);

            return await Client.CoreV1.PatchNamespacedConfigMapAsync(new V1Patch(configMapJson, V1Patch.PatchType.MergePatch), name, KubernetesConfig.Namespace, cancellationToken: cancellationToken);
        }
    }
}