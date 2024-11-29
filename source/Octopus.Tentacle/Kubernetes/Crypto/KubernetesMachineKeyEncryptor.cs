using System;
using System.Diagnostics.CodeAnalysis;
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
        readonly IKubernetesMachineEncryptionKeyProvider encryptionKeyProvider;
        byte[]? key;
        byte[]? iv;

        public KubernetesMachineKeyEncryptor(IKubernetesMachineEncryptionKeyProvider encryptionKeyProvider)
        {
            this.encryptionKeyProvider = encryptionKeyProvider;
        }

        public string Encrypt(string raw)
        {
            EnsureMachineKeyAndIvLoaded();

            using var aes = Aes.Create();
            using var enc = aes.CreateEncryptor(key, iv);
            var inBlock = Encoding.UTF8.GetBytes(raw);
            var trans = enc.TransformFinalBlock(inBlock, 0, inBlock.Length);
            return Convert.ToBase64String(trans);
        }

        public string Decrypt(string encrypted)
        {
            EnsureMachineKeyAndIvLoaded();

            using var aes = Aes.Create();
            using var dec = aes.CreateDecryptor(key, iv);
            var fromBase = Convert.FromBase64String(encrypted);
            var asd = dec.TransformFinalBlock(fromBase, 0, fromBase.Length);
            return Encoding.UTF8.GetString(asd);
        }

        [MemberNotNull(nameof(key), nameof(iv))]
        void EnsureMachineKeyAndIvLoaded()
        {
            //if either is null, load it again
            if (key is null || iv is null)
            {
                (key, iv) = encryptionKeyProvider.GetMachineKey(CancellationToken.None).GetAwaiter().GetResult();
            }
        }
    }
}