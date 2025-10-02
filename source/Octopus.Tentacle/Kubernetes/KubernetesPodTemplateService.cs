using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesPodTemplateService
    {
        Task<ScriptPodTemplateCustomResource?> GetOldestScriptPodTemplateCustomResource(CancellationToken cancellationToken);
        Task<ScriptPodTemplate?> GetScriptPodTemplate(CancellationToken cancellationToken);
    }
    
    public class KubernetesPodTemplateService : KubernetesService, IKubernetesPodTemplateService
    {
        ScriptPodTemplate? CachedPodTemplate {get; set;}
        
        public KubernetesPodTemplateService(IKubernetesClientConfigProvider configProvider, ISystemLog log)
            : base(configProvider, log)
        {}

        public async Task<ScriptPodTemplate?> GetScriptPodTemplate(CancellationToken cancellationToken)
        {
            if (CachedPodTemplate != null) return CachedPodTemplate.Clone();
            
            ScriptPodTemplate? scriptPodTemplate = null;
            
            var scriptPodTemplateCustomResource = await GetOldestScriptPodTemplateCustomResource(cancellationToken);
            var scriptPodTemplateDeployment = await GetOldestScriptPodTemplateDeployment(cancellationToken);

            // Use custom resource first
            if (scriptPodTemplateCustomResource != null)
            {
                scriptPodTemplate = ScriptPodTemplate.GetScriptPodTemplateFromCustomResource(scriptPodTemplateCustomResource);
            } else if (scriptPodTemplateDeployment != null)
            {
                scriptPodTemplate = ScriptPodTemplate.GetScriptPodTemplateFromDeployment(scriptPodTemplateDeployment);
            }
            
            CachedPodTemplate = scriptPodTemplate;
            return scriptPodTemplate.Clone();
        }

        public async Task<ScriptPodTemplateCustomResource?> GetOldestScriptPodTemplateCustomResource(CancellationToken cancellationToken)
        {
            return await RetryPolicy.ExecuteAsync(async () =>
            {
                ScriptPodTemplateCustomResourceList resourceList;
                try
                {
                    resourceList = await Client.CustomObjects.ListNamespacedCustomObjectAsync<ScriptPodTemplateCustomResourceList>(
                        "agent.octopus.com",
                        "v1beta1",
                        KubernetesConfig.Namespace,
                        "scriptpodtemplates",
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    // we are happy to handle all exceptions here and just fallback
                    Log.WarnFormat(ex, "Failed to retrieve 'scriptpodtemplates' custom resource");
                    return null;
                }

                return resourceList.Items.OrderBy(r => r.Metadata.CreationTimestamp).FirstOrDefault();
            });
        }
        
        public async Task<V1Deployment?> GetOldestScriptPodTemplateDeployment(CancellationToken cancellationToken)
        {
            return await RetryPolicy.ExecuteAsync(async () =>
            {
                V1DeploymentList resourceList;
                try
                {
                    resourceList = await Client.AppsV1.ListNamespacedDeploymentAsync(
                        KubernetesConfig.Namespace,
                        labelSelector: OctopusLabels.PodTemplate,
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    // we are happy to handle all exceptions here and just fallback
                    Log.WarnFormat(ex, "Failed to retrieve 'scriptpodtemplates' custom resource");
                    return null;
                }

                return resourceList.Items.OrderBy(r => r.Metadata.CreationTimestamp).FirstOrDefault();
            });
        }
        
        //These are only ever deserialized from JSON
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        class ScriptPodTemplateCustomResourceList : KubernetesObject
        {
            public V1ListMeta Metadata { get; set; }
            public List<ScriptPodTemplateCustomResource> Items { get; set; }
        }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    }
}