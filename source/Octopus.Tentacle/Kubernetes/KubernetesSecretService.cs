using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Autorest;
using k8s.Models;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesSecretService
    {
        Task<V1Secret?> TryGetSecretAsync(string name, CancellationToken cancellationToken);
        Task CreateSecretAsync(V1Secret secret, CancellationToken cancellationToken);
        Task UpdateSecretDataAsync(string secretName, Dictionary<string, byte[]> secretData, CancellationToken cancellationToken);
    }

    public class KubernetesSecretService : KubernetesService, IKubernetesSecretService
    {
        public KubernetesSecretService(IKubernetesClientConfigProvider configProvider)
            : base(configProvider)
        {
        }

        public async Task<V1Secret?> TryGetSecretAsync(string name, CancellationToken cancellationToken)
        {
            try
            {
                return await Client.ReadNamespacedSecretAsync(name, KubernetesConfig.Namespace, cancellationToken: cancellationToken);
            }
            catch (HttpOperationException opException)
                when (opException.Response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task CreateSecretAsync(V1Secret secret, CancellationToken cancellationToken)
            => await Client.CreateNamespacedSecretAsync(secret, KubernetesConfig.Namespace, cancellationToken: cancellationToken);

        public async Task UpdateSecretDataAsync(string secretName, Dictionary<string, byte[]> secretData, CancellationToken cancellationToken)
        {
            var patchSecret = new V1Secret
            {
                Data = secretData
            };

            var patchYaml = KubernetesJson.Serialize(patchSecret);

            await Client.PatchNamespacedSecretAsync(new V1Patch(patchYaml, V1Patch.PatchType.MergePatch), secretName, KubernetesConfig.Namespace, cancellationToken: cancellationToken);
        }
    }
}