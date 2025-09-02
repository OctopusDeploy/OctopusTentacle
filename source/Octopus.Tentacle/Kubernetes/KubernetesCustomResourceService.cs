using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesCustomResourceService
    {
        Task<ScriptPodTemplateCustomResource?> GetOldestScriptPodTemplateCustomResource(CancellationToken cancellationToken);
    }

    public class KubernetesCustomResourceService : KubernetesService, IKubernetesCustomResourceService
    {
        public KubernetesCustomResourceService(IKubernetesClientConfigProvider configProvider, ISystemLog log)
            : base(configProvider, log)
        {
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