using System;
using System.Security.Cryptography;
using System.Text;
using Octopus.Tentacle.Configuration.Crypto;

namespace Octopus.Tentacle.Tests.Configuration.Crypto
{
    /// <summary>
    /// Produces ciphertext the way Linux Tentacles did before <see cref="LinuxMachineKeyEncryptor.ProtectedValuePrefix"/>
    /// was introduced: AES-CBC with the key source's fixed IV, base64, no prefix. Tests use it to fabricate the
    /// configuration an upgraded install brings with it.
    /// </summary>
    public static class LegacySchemeEncryption
    {
        public static string Encrypt(ICryptoKeyNixSource keySource, string plaintext)
            => Encrypt(keySource, Encoding.UTF8.GetBytes(plaintext));

        public static string Encrypt(ICryptoKeyNixSource keySource, byte[] plaintext)
        {
            var (key, iv) = keySource.Load();
            using var aes = Aes.Create();
            using var encryptor = aes.CreateEncryptor(key, iv);
            return Convert.ToBase64String(encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length));
        }
    }

    /// <summary>
    /// An <see cref="IMachineKeyEncryptor"/> that writes values in the pre-prefix format, for seeding a key-value
    /// store with what an earlier version of Tentacle would have written.
    /// </summary>
    public class LegacySchemeEncryptor : IMachineKeyEncryptor
    {
        readonly ICryptoKeyNixSource keySource;

        public LegacySchemeEncryptor(ICryptoKeyNixSource keySource)
        {
            this.keySource = keySource;
        }

        public string Encrypt(string raw)
            => LegacySchemeEncryption.Encrypt(keySource, raw);

        public string Decrypt(string encrypted)
        {
            var (key, iv) = keySource.Load();
            using var aes = Aes.Create();
            using var decryptor = aes.CreateDecryptor(key, iv);
            var payload = Convert.FromBase64String(encrypted);
            return Encoding.UTF8.GetString(decryptor.TransformFinalBlock(payload, 0, payload.Length));
        }

        public bool RequiresReEncryption(string encrypted)
            => false;
    }
}
