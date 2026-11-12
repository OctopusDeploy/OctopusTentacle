using System;
using System.Security.Cryptography;
using System.Text;
using Octopus.Tentacle.Configuration.Crypto;

namespace Octopus.Tentacle.Tests.Configuration.Crypto
{
    public class LegacySchemeEncryptor : IMachineKeyEncryptor
    {
        readonly ICryptoKeyNixSource keySource;

        public LegacySchemeEncryptor(ICryptoKeyNixSource keySource)
        {
            this.keySource = keySource;
        }

        public string Encrypt(string raw)
        {
            var plaintext = Encoding.UTF8.GetBytes(raw);
            var (key, iv) = keySource.Load();
            using var aes = Aes.Create();
            using var encryptor = aes.CreateEncryptor(key, iv);
            return Convert.ToBase64String(encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length));
        }

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
