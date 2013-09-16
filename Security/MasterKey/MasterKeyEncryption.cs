using System;
using System.IO;
using System.Security.Cryptography;
using Octopus.Platform.Model;
using Octopus.Shared.Bcl.IO;

namespace Octopus.Shared.Security.MasterKey
{
    public static class MasterKeyEncryption
    {
        public const int KeySizeBits = 128, BlockSizeBits = 128, IVSizeBits = BlockSizeBits;

        public static byte[] GenerateKey()
        {
            var key = new byte[KeySizeBits / 8];
            using (var provider = new RNGCryptoServiceProvider())
                provider.GetBytes(key);
            return key;
        }

        public static SymmetricAlgorithm CreateAlgorithm(byte[] key, bool generateSalt = false)
        { 
            // If generateSalt is true, we'll let the algorithm generate the salt itself,
            // since that will use the underlying provider.

            var algorithm = new AesCryptoServiceProvider
            {
                Padding = PaddingMode.PKCS7, 
                KeySize = KeySizeBits, 
                Key = key, 
                BlockSize = BlockSizeBits, 
                Mode = CipherMode.CBC,
            };

            if (!generateSalt)
                algorithm.IV = new byte[BlockSizeBits / 8];

            return algorithm;
        }

        public static Type AlgorithmType
        {
            get
            {
                return typeof(AesCryptoServiceProvider);
            }
        }

        public static EncryptedBytes ToCiphertext(byte[] masterKey, byte[] plaintext)
        {
            if (masterKey == null) throw new ArgumentNullException("masterKey");
            if (plaintext == null) throw new ArgumentNullException("plaintext");

            using (var algorithm = CreateAlgorithm(masterKey, generateSalt: true))
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

        public static byte[] ToPlaintext(byte[] masterKey, EncryptedBytes encrypted)
        {
            if (masterKey == null) throw new ArgumentNullException("masterKey");
            if (encrypted == null) throw new ArgumentNullException("encrypted");

            using (var algorithm = CreateAlgorithm(masterKey))
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

        public static Stream ReadAsCiphertext(byte[] masterKey, Stream plaintext)
        {
            if (masterKey == null) throw new ArgumentNullException("masterKey");
            if (plaintext == null) throw new ArgumentNullException("plaintext");

            // This can be done lazily by creating a "salted stream" class that prepends the salt, but
            // for now we'll buffer it.

            using (var algorithm = CreateAlgorithm(masterKey, generateSalt: true))
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

        public static Stream ReadAsPlaintext(byte[] masterKey, Stream ciphertext)
        {
            if (masterKey == null) throw new ArgumentNullException("masterKey");
            if (ciphertext == null) throw new ArgumentNullException("ciphertext");

            var salt = new byte[IVSizeBits / 8];
            var read = ciphertext.Read(salt, 0, salt.Length);
            if (read != IVSizeBits / 8)
                throw new InvalidOperationException("The ciphertext stream does not contain a salt value");

            var algorithm = CreateAlgorithm(masterKey);
            algorithm.IV = salt;

            var decryptor = algorithm.CreateDecryptor();
            return new StreamDisposalChain(new CryptoStream(ciphertext, decryptor, CryptoStreamMode.Read), decryptor, algorithm);
        }

        public static Stream WriteCiphertextTo(byte[] masterKey, Stream stream)
        {
            if (masterKey == null) throw new ArgumentNullException("masterKey");
            if (stream == null) throw new ArgumentNullException("stream");

            var algorithm = CreateAlgorithm(masterKey, generateSalt: true);
            var salt = algorithm.IV;
            stream.Write(salt, 0, salt.Length);

            var encryptor = algorithm.CreateEncryptor();
            return new StreamDisposalChain(new CryptoStream(stream, encryptor, CryptoStreamMode.Write), encryptor, algorithm);
        }

    }
}
