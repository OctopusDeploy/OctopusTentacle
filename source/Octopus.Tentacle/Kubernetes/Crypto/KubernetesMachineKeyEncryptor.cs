using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Octopus.Tentacle.Configuration.Crypto;

namespace Octopus.Tentacle.Kubernetes.Crypto
{
    public interface IKubernetesMachineKeyEncryptor : IMachineKeyEncryptor
    {
    }

    public class KubernetesMachineKeyEncryptor : IKubernetesMachineKeyEncryptor
    {
        readonly Lazy<(byte[] Key, byte[] Iv)> machineKey;

        public KubernetesMachineKeyEncryptor(IKubernetesMachineEncryptionKeyProvider encryptionKeyProvider)
        {
            machineKey = new Lazy<(byte[] Key, byte[] Iv)>(() => encryptionKeyProvider.GetMachineKey(CancellationToken.None).GetAwaiter().GetResult());
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
    }
}