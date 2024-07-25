using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using k8s.Models;
using Octopus.Diagnostics;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Configuration.Crypto
{
    public interface IKubernetesMachineKeyEncryptor : IMachineKeyEncryptor
    {
    }

    public class KubernetesMachineKeyEncryptor : IKubernetesMachineKeyEncryptor
    {
        const string SecretName = "tentacle-secret";
        const string MachineKeyName = "machine-key";
        const string MachineIvName = "machine-iv";

        readonly IKubernetesSecretService kubernetesSecretService;
        readonly ISystemLog log;

        readonly Lazy<(byte[] Key, byte[] Iv)> machineKey;
        readonly Lazy<V1Secret> secret;

        public KubernetesMachineKeyEncryptor(IKubernetesSecretService kubernetesSecretService, ISystemLog log)
        {
            this.kubernetesSecretService = kubernetesSecretService;
            this.log = log;
            secret = new Lazy<V1Secret>(GetSecret);
            machineKey = new Lazy<(byte[] Key, byte[] Iv)>(GetMachineKey);
        }

        public string Encrypt(string raw)
        {
            using var aes = Aes.Create();
            using var enc = aes.CreateEncryptor(machineKey.Value.Key, machineKey.Value.Iv);
            var inBlock = Encoding.UTF8.GetBytes(raw);
            var trans = enc.TransformFinalBlock(inBlock, 0, inBlock.Length);
            return Convert.ToBase64String(trans);
        }

        public string Decrypt(string encrypted)
        {
            using var aes = Aes.Create();
            using var dec = aes.CreateDecryptor(machineKey.Value.Key, machineKey.Value.Iv);
            var fromBase = Convert.FromBase64String(encrypted);
            var asd = dec.TransformFinalBlock(fromBase, 0, fromBase.Length);
            return Encoding.UTF8.GetString(asd);
        }
        V1Secret GetSecret()
        {
            return kubernetesSecretService.TryGetSecretAsync(SecretName, CancellationToken.None).GetAwaiter().GetResult() 
                ?? throw new InvalidOperationException($"Unable to retrieve MachineKey from secret for namespace {KubernetesAgentDetection.Namespace}");
        }

        (byte[] key, byte[] iv) GetMachineKey()
        {
            var data = secret.Value.Data;
            if (data is null ||
                !data.TryGetValue(MachineKeyName, out var key) ||
                !data.TryGetValue(MachineIvName, out var iv))
            {
                (key, iv) = GenerateMachineKey(log);
                data = new Dictionary<string, byte[]> { { MachineKeyName, key }, { MachineIvName, iv } };

                kubernetesSecretService.UpdateSecretDataAsync(SecretName, data, CancellationToken.None).GetAwaiter().GetResult();
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