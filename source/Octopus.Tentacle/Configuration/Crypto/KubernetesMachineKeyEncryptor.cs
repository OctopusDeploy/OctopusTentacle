using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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

        readonly byte[]? machineKey;
        readonly byte[]? machineIv;
        public KubernetesMachineKeyEncryptor(IKubernetesSecretService kubernetesSecretService, ISystemLog log)
        {
            var secret = kubernetesSecretService.TryGet(SecretName, CancellationToken.None).GetAwaiter().GetResult() ?? throw new InvalidOperationException($"Unable to retrieve MachineKey from secret for namespace {KubernetesConfig.Namespace}");

            if (secret.Data is null ||
                !secret.Data.TryGetValue(MachineKeyName, out machineKey) ||
                !secret.Data.TryGetValue(MachineIvName, out machineIv))
            {
                var (key, iv) = GenerateMachineKey(log);
                var data = new Dictionary<string, byte[]> { { MachineKeyName, machineKey = key }, { MachineIvName, machineIv = iv } };

                kubernetesSecretService.Patch(SecretName, data, CancellationToken.None).GetAwaiter().GetResult();
            }

            if (machineKey == null || machineIv == null)
            {
                throw new InvalidOperationException("Unable to retrieve or create a machine key for encryption.");
            }
        }

        public string Encrypt(string raw)
        {
            using var aes = Aes.Create();
            using var enc = aes.CreateEncryptor(machineKey!, machineIv);
            var inBlock = Encoding.UTF8.GetBytes(raw);
            var trans = enc.TransformFinalBlock(inBlock, 0, inBlock.Length);
            return Convert.ToBase64String(trans);
        }

        public string Decrypt(string encrypted)
        {
            using var aes = Aes.Create();
            using var dec = aes.CreateDecryptor(machineKey!, machineIv);
            var fromBase = Convert.FromBase64String(encrypted);
            var asd = dec.TransformFinalBlock(fromBase, 0, fromBase.Length);
            return Encoding.UTF8.GetString(asd);
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