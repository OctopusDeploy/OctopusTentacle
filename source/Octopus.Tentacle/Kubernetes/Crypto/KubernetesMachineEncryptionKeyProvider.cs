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
        static string SecretName => KubernetesConfig.TentacleEncryptionSecretName;
        const string MachineKeyName = "machine-key";
        const string MachineIvName = "machine-iv";

        readonly IKubernetesSecretService secretService;
        readonly ISystemLog log;
        V1Secret? secret;
        readonly SemaphoreSlim accessSemaphore;

        public KubernetesMachineEncryptionKeyProvider(IKubernetesSecretService secretService, ISystemLog log)
        {
            this.secretService = secretService;
            this.log = log;
            accessSemaphore = new SemaphoreSlim(1, 1);
        }

        public async Task<(byte[] key, byte[] iv)> GetMachineKey(CancellationToken cancellationToken)
        {
            //We lock access to avoid 2 threads creating the machine key twice
            await accessSemaphore.WaitAsync(cancellationToken);

            try
            {
                secret ??= await secretService.TryGetSecretAsync(SecretName, CancellationToken.None);

                if (secret is null)
                    throw new InvalidOperationException($"Unable to retrieve MachineKey from secret for namespace {KubernetesConfig.Namespace}");

                var data = secret.Data;
                if (data is null ||
                    !data.TryGetValue(MachineKeyName, out var key) ||
                    !data.TryGetValue(MachineIvName, out var iv))
                {
                    (key, iv) = GenerateMachineKey(log);
                    data = new Dictionary<string, byte[]> { { MachineKeyName, key }, { MachineIvName, iv } };

                    //make sure we update the local secret with the updated data 
                    secret = await secretService.UpdateSecretDataAsync(SecretName, data, CancellationToken.None);
                }

                if (key == null || iv == null)
                {
                    throw new InvalidOperationException("Unable to retrieve or create a machine key for encryption.");
                }

                return (key, iv);
            }
            finally
            {
                accessSemaphore.Release();
            }
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