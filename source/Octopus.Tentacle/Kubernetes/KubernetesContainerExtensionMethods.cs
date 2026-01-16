using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using k8s;
using k8s.Models;

namespace Octopus.Tentacle.Kubernetes
{
    public static class KubernetesContainerExtensionMethods
    {
        [return: NotNullIfNotNull(nameof(source))]
        public static T? Clone<T>(this T? source)
        {
            if (source is null)
            {
                return default;
            }
            
            // Use JSON serialization for deep cloning
            var json = KubernetesJson.Serialize(source);
            return KubernetesJson.Deserialize<T>(json);
        }
    }
}