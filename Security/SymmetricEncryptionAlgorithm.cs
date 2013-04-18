using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace Octopus.Shared.Security
{
    public abstract class SymmetricEncryptionAlgorithm : EncryptionAlgorithm
    {
        private static readonly Encoding Encoding = Encoding.UTF8;

        public override EncryptResult Encrypt(string plainText)
        {
            var salt = CreateSalt();
            var plainTextBytes = Encoding.GetBytes(plainText);

            using (var algorithm = CreateAlgorithm(salt))
            using (var memoryStream = new MemoryStream())
            using (var encryptor = algorithm.CreateEncryptor())
            {
                using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                {
                    cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                }

                return new EncryptResult(memoryStream.ToArray(), salt);
            }
        }

        public override string Decrypt(byte[] cipherText, byte[] salt)
        {
            using (var algorithm = CreateAlgorithm(salt))
            using (var memoryStream = new MemoryStream())
            using (var decryptor = algorithm.CreateDecryptor())
            {
                using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Write))
                {
                    cryptoStream.Write(cipherText, 0, cipherText.Length);
                }

                return Encoding.GetString(memoryStream.ToArray());
            }
        }

        private static byte[] CreateSalt()
        {
            var salt = new byte[32];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
            }
            return salt;
        }

        protected abstract SymmetricAlgorithm CreateAlgorithm(byte[] salt);
    }
}