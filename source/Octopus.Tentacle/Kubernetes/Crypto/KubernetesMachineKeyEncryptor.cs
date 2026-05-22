using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Tentacle.Configuration.Crypto;

namespace Octopus.Tentacle.Kubernetes.Crypto
{
    public interface IKubernetesMachineKeyEncryptor : IMachineKeyEncryptor
    {
        Task InitializeAsync(CancellationToken cancellationToken);
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

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            (key, iv) = await encryptionKeyProvider.GetMachineKey(cancellationToken);
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
            var decryptedBytes = dec.TransformFinalBlock(fromBase, 0, fromBase.Length);
            return Encoding.UTF8.GetString(decryptedBytes);
        }

        [MemberNotNull(nameof(key), nameof(iv))]
        void EnsureMachineKeyAndIvLoaded()
        {
            if (key is null || iv is null)
                throw new InvalidOperationException("KubernetesMachineKeyEncryptor must be initialized by calling InitializeAsync before use.");
        }
    }
}