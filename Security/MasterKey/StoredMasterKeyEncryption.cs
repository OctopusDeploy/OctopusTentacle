using System;
using System.IO;
using System.Security.Cryptography;
using Octopus.Platform.Deployment.Configuration;
using Octopus.Platform.Model;
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
                {
                    using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        cryptoStream.Write(plaintext, 0, plaintext.Length);
                    }

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

        public Stream ReadAsCiphertext(Stream plaintext)
        {
            if (plaintext == null) throw new ArgumentNullException("plaintext");

            // This can be done lazily by creating a "salted stream" class that prepends the salt, but
            // for now we'll buffer it.

            using (var algorithm = MasterKeyEncryption.CreateAlgorithm(storageConfiguration.MasterKey, generateSalt: true))
            {
                var salt = algorithm.IV;

                var result = new MemoryStream();
                result.Write(salt, 0, salt.Length);

                using (var encryptor = algorithm.CreateEncryptor())
                {
                    using (var cryptoStream = new CryptoStream(plaintext, encryptor, CryptoStreamMode.Read))
                        cryptoStream.CopyTo(result);

                    result.Position = 0;
                    return result;
                }
            }
        }

        public Stream ReadAsPlaintext(Stream ciphertext)
        {
            if (ciphertext == null) throw new ArgumentNullException("ciphertext");

            var salt = new byte[MasterKeyEncryption.IVSizeBits / 8];
            var read = ciphertext.Read(salt, 0, salt.Length);
            if (read != MasterKeyEncryption.IVSizeBits / 8)
                throw new InvalidOperationException("The ciphertext stream does not contain a salt value");

            using (var algorithm = MasterKeyEncryption.CreateAlgorithm(storageConfiguration.MasterKey))
            {
                algorithm.IV = salt;

                var decryptor = algorithm.CreateDecryptor(); // Let this get GC'd
                return new CryptoStream(ciphertext, decryptor, CryptoStreamMode.Read);
            }
        }

        public Stream WriteCiphertextTo(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException("stream");

            using (var algorithm = MasterKeyEncryption.CreateAlgorithm(storageConfiguration.MasterKey, generateSalt: true))
            {
                var salt = algorithm.IV;
                stream.Write(salt, 0, salt.Length);

                var encryptor = algorithm.CreateEncryptor(); // Let this get GC'd
                return new CryptoStream(stream, encryptor, CryptoStreamMode.Write);
            }
        }
    }
}
