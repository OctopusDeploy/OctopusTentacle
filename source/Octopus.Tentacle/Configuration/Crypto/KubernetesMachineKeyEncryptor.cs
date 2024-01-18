using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using k8s;
using k8s.Models;
using Octopus.Diagnostics;
using Octopus.Tentacle.Kubernetes;

namespace Octopus.Tentacle.Configuration.Crypto
{
    public class KubernetesMachineKeyEncryptor : IMachineKeyEncryptor
    {
        const string SecretName = "tentacle-secret";
        const string MachineKeyName = "machine-key";
        const string MachineIvName = "machine-iv";

        readonly byte[]? machineKey;
        readonly byte[]? machineIv;
        public KubernetesMachineKeyEncryptor(ILog log)
        {
            var @namespace = KubernetesConfig.Namespace;
            var kubeConfig = KubernetesClientConfiguration.InClusterConfig();
            var client = new k8s.Kubernetes(kubeConfig);

            var secret = CreateSecret(@namespace, client, log);

            if (!secret.Data.TryGetValue(MachineKeyName, out machineKey) ||
                !secret.Data.TryGetValue(MachineIvName, out machineIv))
            {
                var (key, iv) = GenerateMachineKey(log);
                secret.Data[MachineKeyName] = machineKey = key;
                secret.Data[MachineIvName] = machineIv = iv;

                client.CoreV1.ReplaceNamespacedSecret(secret, SecretName, @namespace);
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

        static V1Secret CreateSecret(string @namespace, AbstractKubernetes client, ILog log)
        {
            V1Secret? secret;
            try
            {
                secret = client.CoreV1.ReadNamespacedSecret(SecretName, @namespace);
            }
            catch
            {
                log.Info($"Secret for Tentacle Configuration not found for namespace {@namespace}, creating new Secret.");
                secret = null;
            }

            if (secret is not null)
                return secret;

            var (key, iv) = GenerateMachineKey(log);
            return client.CoreV1.CreateNamespacedSecret(
                new V1Secret
                {
                    Metadata = new V1ObjectMeta { Name = SecretName, NamespaceProperty = @namespace },
                    Data = new Dictionary<string, byte[]> { { MachineKeyName, key }, {MachineIvName, iv} }
                }, @namespace);
        }
    }
}