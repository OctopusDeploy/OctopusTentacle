using System;
using System.Net;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Octopus.Tentacle.Core.Diagnostics;

namespace Octopus.Tentacle.Kubernetes.Tests.Integration.Support
{

    // This is a copy of the production ConfigMapService, but allows the namespace to be explicitly
    // defined.
    public class TestSupportConfigMapService : KubernetesService, IKubernetesConfigMapService
    {
        readonly string targetNamespace;

        public TestSupportConfigMapService(IKubernetesClientConfigProvider configProvider, ISystemLog log, string targetNamespace)
            : base(configProvider, log)
        {
            this.targetNamespace = targetNamespace;
        }

        public async Task<V1ConfigMap?> TryGet(string name, CancellationToken cancellationToken)
        {
            return await RetryPolicy.ExecuteAsync(async () =>
            {
                try
                {
                    return await Client.CoreV1.ReadNamespacedConfigMapAsync(name, targetNamespace, cancellationToken: cancellationToken);
                }
                catch (HttpOperationException opException)
                    when (opException.Response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
            });
        }

        public async Task<V1ConfigMap> Patch(string name, IDictionary<string, string> data, CancellationToken cancellationToken)
        {
            var configMap = new V1ConfigMap
            {
                Data = data
            };

            var configMapJson = KubernetesJson.Serialize(configMap);

            return await RetryPolicy.ExecuteAsync(async () =>
                await Client.CoreV1.PatchNamespacedConfigMapAsync(new V1Patch(configMapJson, V1Patch.PatchType.MergePatch), name, targetNamespace, cancellationToken: cancellationToken));
        }
    }
}