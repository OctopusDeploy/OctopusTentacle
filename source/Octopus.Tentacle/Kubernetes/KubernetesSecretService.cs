using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Octopus.Diagnostics;
using Octopus.Tentacle.Time;
using Polly;
using Polly.Retry;

namespace Octopus.Tentacle.Kubernetes
{
    public interface IKubernetesSecretService
    {
        Task<V1Secret?> TryGetSecretAsync(string name, CancellationToken cancellationToken);
        Task CreateSecretAsync(V1Secret secret, CancellationToken cancellationToken);
        Task UpdateSecretDataAsync(string secretName, IDictionary<string, byte[]> secretData, CancellationToken cancellationToken);
    }

    public class KubernetesSecretService : KubernetesService, IKubernetesSecretService
    {
        readonly AsyncRetryPolicy retryPolicy;
        const int MaxDurationSeconds = 30;

        public KubernetesSecretService(IKubernetesClientConfigProvider configProvider, ISystemLog log)
            : base(configProvider)
        {
            retryPolicy = Policy.Handle<Exception>().WaitAndRetryAsync(5,
                retry => TimeSpan.FromSeconds(ExponentialBackoff.GetDuration(retry, MaxDurationSeconds)),
                (ex, duration) =>
                {
                    log.Verbose(ex, "An unexpected error occured while querying Pod logs, waiting for: " + duration);
                });
        }

        public async Task<V1Secret?> TryGetSecretAsync(string name, CancellationToken cancellationToken)
        {
            return await retryPolicy.ExecuteAsync(async () =>
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
            });
        }

        public async Task CreateSecretAsync(V1Secret secret, CancellationToken cancellationToken)
        {
            AddStandardMetadata(secret);
            await retryPolicy.ExecuteAsync(async () => 
                await Client.CreateNamespacedSecretAsync(secret, KubernetesConfig.Namespace, cancellationToken: cancellationToken));
        }

        public async Task UpdateSecretDataAsync(string secretName, IDictionary<string, byte[]> secretData, CancellationToken cancellationToken)
        {
            var patchSecret = new V1Secret
            {
                Data = secretData
            };

            var patchYaml = KubernetesJson.Serialize(patchSecret);

            await retryPolicy.ExecuteAsync(async () => 
                await Client.PatchNamespacedSecretAsync(new V1Patch(patchYaml, V1Patch.PatchType.MergePatch), secretName, KubernetesConfig.Namespace, cancellationToken: cancellationToken));
        }
    }
}