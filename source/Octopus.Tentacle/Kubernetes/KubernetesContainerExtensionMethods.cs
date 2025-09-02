using System.Diagnostics.CodeAnalysis;
using k8s;
using k8s.Models;

namespace Octopus.Tentacle.Kubernetes
{
    public static class KubernetesContainerExtensionMethods
    {
        [return: NotNullIfNotNull(nameof(source))]
        public static V1Container? Clone(this V1Container? source)
        {
            if (source is null)
            {
                return null;
            }
            // Use JSON serialization for deep cloning
            var json = KubernetesJson.Serialize(source);
            return KubernetesJson.Deserialize<V1Container>(json);
        }
    }
}