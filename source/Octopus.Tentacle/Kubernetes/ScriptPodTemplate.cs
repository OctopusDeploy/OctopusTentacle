using System.Linq;
using System.Text.Json.Serialization;
using k8s.Models;

namespace Octopus.Tentacle.Kubernetes
{
    public class ScriptPodTemplate
    {
        [JsonPropertyName("podMetadata")]
        public PodMetadata? PodMetadata { get; set; }

        [JsonPropertyName("podSpec")]
        public V1PodSpec? PodSpec { get; set; }

        [JsonPropertyName("scriptContainerSpec")]
        public V1Container? ScriptContainerSpec { get; set; }
        
        [JsonPropertyName("scriptInitContainerSpec")]
        public V1Container? ScriptInitContainerSpec { get; set; }

        [JsonPropertyName("watchdogContainerSpec")]
        public V1Container? WatchdogContainerSpec { get; set; }

        public static ScriptPodTemplate? GetScriptPodTemplateFromDeployment(V1Deployment deployment)
        {
            var template = new ScriptPodTemplate
            {
                PodMetadata = new PodMetadata
                {
                    Labels = deployment.Spec.Template.Metadata.Labels,
                    Annotations = deployment.Spec.Template.Metadata.Annotations,
                },
                PodSpec = deployment.Spec.Template.Spec.Clone(),
                ScriptContainerSpec = deployment.Spec.Template.Spec.Containers.First(c => c.Name == ContainerNames.PodTemplateScriptContainerName).Clone(),
                ScriptInitContainerSpec = deployment.Spec.Template.Spec.Containers.First(c => c.Name == ContainerNames.PodTemplateScriptContainerName).Clone(),
                WatchdogContainerSpec = deployment.Spec.Template.Spec.Containers.First(c => c.Name == ContainerNames.PodTemplateWatchdogContainerName).Clone()
            };
            
            // The deployment will have the containers, we should not pull them in here though - we overwrite them and programatically create them later
            if (template.PodSpec != null) {
                template.PodSpec.Containers = null;
            }
            return template;
        }

        public static ScriptPodTemplate? GetScriptPodTemplateFromCustomResource(ScriptPodTemplateCustomResource scriptPodTemplateCustomResource)
        {
            var template = new ScriptPodTemplate
            {
                PodMetadata = scriptPodTemplateCustomResource.Spec.PodMetadata,
                PodSpec = scriptPodTemplateCustomResource.Spec.PodSpec,
                ScriptContainerSpec = scriptPodTemplateCustomResource.Spec.ScriptContainerSpec.Clone(),
                ScriptInitContainerSpec = scriptPodTemplateCustomResource.Spec.ScriptContainerSpec.Clone(),
                WatchdogContainerSpec = scriptPodTemplateCustomResource.Spec.WatchdogContainerSpec.Clone()
            };
            return template;
        }

    }
    
}
