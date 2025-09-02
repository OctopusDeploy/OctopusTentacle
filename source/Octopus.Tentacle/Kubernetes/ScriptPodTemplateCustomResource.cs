using System.Text.Json.Serialization;
using k8s;
using k8s.Models;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace Octopus.Tentacle.Kubernetes
{
    public class ScriptPodTemplateCustomResource : KubernetesObject, IMetadata<V1ObjectMeta>
    {
        [JsonPropertyName("metadata")]
        public V1ObjectMeta Metadata { get; set; }

        [JsonPropertyName("spec")]
        public ScriptPodTemplateSpec Spec { get; set; }
        
        [JsonPropertyName("status")]
        public ScriptPodTemplateStatus Status { get; set; }
    }

    public class ScriptPodTemplateSpec
    {
        [JsonPropertyName("podMetadata")]
        public V1ObjectMeta? PodMetadata { get; set; }
        
        [JsonPropertyName("podSpec")]
        public V1PodSpec PodSpec { get; set; }

        [JsonPropertyName("scriptContainerSpec")]
        public V1Container? ScriptContainerSpec { get; set; }

        [JsonPropertyName("watchdogContainerSpec")]
        public V1Container? WatchdogContainerSpec { get; set; }
    }

    public class ScriptPodTemplateStatus
    {
    }
}