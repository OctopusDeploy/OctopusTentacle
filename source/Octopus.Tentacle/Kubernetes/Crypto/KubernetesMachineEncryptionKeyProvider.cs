using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using k8s.Models;
using Nito.AsyncEx;
using Octopus.Diagnostics;

namespace Octopus.Tentacle.Kubernetes.Crypto
{
    public interface IKubernetesMachineEncryptionKeyProvider
    {
        Task<(byte[] key, byte[] iv)> GetMachineKey(CancellationToken cancellationToken);
    }

    public class KubernetesMachineEncryptionKeyProvider : IKubernetesMachineEncryptionKeyProvider
    {
        const string SecretName = "tentacle-secret";
        const string MachineKeyName = "machine-key";
        const string MachineIvName = "machine-iv";

        readonly IKubernetesSecretService secretService;
        readonly ISystemLog log;
        readonly AsyncLazy<V1Secret> secret;

        public KubernetesMachineEncryptionKeyProvider(IKubernetesSecretService secretService, ISystemLog log)
        {
            this.secretService = secretService;
            this.log = log;
            secret = new AsyncLazy<V1Secret>(GetSecret);
        }

        async Task<V1Secret> GetSecret()
        {
            return await secretService.TryGetSecretAsync(SecretName, CancellationToken.None)
                ?? throw new InvalidOperationException($"Unable to retrieve MachineKey from secret for namespace {KubernetesConfig.Namespace}");
        }

        public async Task<(byte[] key, byte[] iv)> GetMachineKey(CancellationToken cancellationToken)
        {
            var data = (await secret).Data;
            if (data is null ||
                !data.TryGetValue(MachineKeyName, out var key) ||
                !data.TryGetValue(MachineIvName, out var iv))
            {
                (key, iv) = GenerateMachineKey(log);
                data = new Dictionary<string, byte[]> { { MachineKeyName, key }, { MachineIvName, iv } };

                await secretService.UpdateSecretDataAsync(SecretName, data, CancellationToken.None);
            }

            if (key == null || iv == null)
            {
                throw new InvalidOperationException("Unable to retrieve or create a machine key for encryption.");
            }

            return (key, iv);
        }

        static (byte[] key, byte[] iv) GenerateMachineKey(ILog log)
        {
            log.Info("Machine key file does not yet exist. Generating key that will be used to encrypt data for this tentacle.");
            var aes = Aes.Create();
            aes.GenerateIV();
            aes.GenerateKey();
            return (aes.Key, aes.IV);
        }
    }
}