using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesSecretService
    {
        Task<V1Secret?> TryGetSecretAsync(string name, CancellationToken cancellationToken);
        Task CreateSecretAsync(V1Secret secret, CancellationToken cancellationToken);
        Task<V1Secret> UpdateSecretDataAsync(string secretName, IDictionary<string, byte[]> secretData, CancellationToken cancellationToken);
    }

    public class KubernetesSecretService : KubernetesService, IKubernetesSecretService
    {
        public KubernetesSecretService(IKubernetesClientConfigProvider configProvider, IKubernetesConfiguration kubernetesConfiguration, ISystemLog log)
            : base(configProvider,kubernetesConfiguration, log)
        {
        }

        public async Task<V1Secret?> TryGetSecretAsync(string name, CancellationToken cancellationToken)
        {
            return await RetryPolicy.ExecuteAsync(async () =>
            {
                try
                {
                    return await Client.ReadNamespacedSecretAsync(name, Namespace, cancellationToken: cancellationToken);
                }
                catch (HttpOperationException opException)
                    when (opException.Response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
            });
        }

        public async Task CreateSecretAsync(V1Secret secret, CancellationToken cancellationToken)
        {
            AddStandardMetadata(secret);
          
            //We only want to retry read/modify operations for now (since they are idempotent)
            await Client.CreateNamespacedSecretAsync(secret, Namespace, cancellationToken: cancellationToken);
        }

        public async Task<V1Secret> UpdateSecretDataAsync(string secretName, IDictionary<string, byte[]> secretData, CancellationToken cancellationToken)
        {
            var patchSecret = new V1Secret
            {
                Data = secretData
            };

            var patchYaml = KubernetesJson.Serialize(patchSecret);

            return await RetryPolicy.ExecuteAsync(async () =>
                await Client.PatchNamespacedSecretAsync(new V1Patch(patchYaml, V1Patch.PatchType.MergePatch), secretName, Namespace, cancellationToken: cancellationToken));
        }
    }
}