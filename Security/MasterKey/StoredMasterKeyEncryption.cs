using System;
using System.IO;
using System.Security.Cryptography;
using Octopus.Platform.Deployment.Configuration;
using Octopus.Platform.Model;
using Octopus.Platform.Security;
using Octopus.Platform.Security.MasterKey;

namespace Octopus.Shared.Security.MasterKey
{
    public class StoredMasterKeyEncryption : IMasterKeyEncryption
    {
        readonly IOctopusServerStorageConfiguration storageConfiguration;

        public StoredMasterKeyEncryption(IOctopusServerStorageConfiguration storageConfiguration)
        {
            this.storageConfiguration = storageConfiguration;
        }

        public EncryptedBytes ToCiphertext(byte[] plaintext)
        {
            if (plaintext == null) throw new ArgumentNullException("plaintext");

            using (var algorithm = MasterKeyEncryption.CreateAlgorithm(storageConfiguration.MasterKey, generateSalt: true))
            {
                var salt = algorithm.IV;

                using (var encryptor = algorithm.CreateEncryptor())
                using (var memoryStream = new MemoryStream())
                using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                {
                    cryptoStream.Write(plaintext, 0, plaintext.Length);
                    return new EncryptedBytes(memoryStream.ToArray(), salt);
                }
            }
        }

        public byte[] ToPlaintext(EncryptedBytes encrypted)
        {
            if (encrypted == null) throw new ArgumentNullException("encrypted");

            using (var algorithm = MasterKeyEncryption.CreateAlgorithm(storageConfiguration.MasterKey))
            {
                algorithm.IV = encrypted.Salt;

                using (var memoryStream = new MemoryStream())
                using (var decryptor = algorithm.CreateDecryptor())
                {
                    using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Write))
                    {
                        cryptoStream.Write(encrypted.Ciphertext, 0, encrypted.Ciphertext.Length);
                    }

                    return memoryStream.ToArray();
                }
            }
        }
    }
}
