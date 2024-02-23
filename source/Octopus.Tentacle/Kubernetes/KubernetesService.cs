using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using k8s;
using k8s.Autorest;
using k8s.Models;
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

        /// <summary>
        /// Adds standard metadata to this <see cref="IKubernetesObject{TMetadata}"/>
        /// </summary>
        /// <param name="k8sObject">The Kubernetes object to add the metadata to.</param>
        protected void AddStandardMetadata(IKubernetesObject<V1ObjectMeta> k8sObject)
        {
            //Everything should be in the main namespace
            k8sObject.Metadata.NamespaceProperty = KubernetesConfig.Namespace;

            //Add helm specific metadata so it's removed if the helm release is uninstalled
            k8sObject.Metadata.Annotations ??= new Dictionary<string, string>();
            k8sObject.Metadata.Annotations["meta.helm.sh/release-name"] = KubernetesConfig.HelmReleaseName;
            k8sObject.Metadata.Annotations["meta.helm.sh/release-namespace"] = KubernetesConfig.Namespace;

            k8sObject.Metadata.Labels ??= new Dictionary<string, string>();
            k8sObject.Metadata.Labels["app.kubernetes.io/managed-by"] = "Helm";
        }

        protected async Task<T?> TryGetAsync<T>(Func<Task<T>> loadAction) where T: class
        {
            try
            {
                return await loadAction();
            }
            catch (HttpOperationException opException)
                when (opException.Response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }
    }
}
