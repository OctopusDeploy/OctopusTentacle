using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Octopus.Tentacle.Security
{
    public class AesEncryption
    {
        private const int PasswordSaltIterations = 1000;
        private static readonly byte[] PasswordPaddingSalt = Encoding.UTF8.GetBytes("Octopuss");
        private static readonly byte[] IvPrefix = Encoding.UTF8.GetBytes("IV__");

        private readonly byte[] key;

        public AesEncryption(string password)
        {
            key = GetEncryptionKey(password);
        }

        public string Decrypt(byte[] encrypted)
        {
            using (var ms = new MemoryStream(encrypted))
            {
                var iv = new byte[16];
                ms.Position = IvPrefix.Length;
                ms.Read(iv, 0, iv.Length);

                using (var algorithm = GetCryptoProvider(iv))
                using (var dec = algorithm.CreateDecryptor())
                using (var cs = new CryptoStream(ms, dec, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs, Encoding.UTF8))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        public byte[] Encrypt(string plaintext)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plaintext);
            using (var algorithm = GetCryptoProvider())
            using (var cryptoTransform = algorithm.CreateEncryptor())
            using (var stream = new MemoryStream())
            {
                // The IV is randomly generated each time so safe to append
                stream.Write(IvPrefix, 0, IvPrefix.Length);
                stream.Write(algorithm.IV, 0, algorithm.IV.Length);

                using (var cs = new CryptoStream(stream, cryptoTransform, CryptoStreamMode.Write))
                {
                    cs.Write(plainTextBytes, 0, plainTextBytes.Length);
                }

                /*
                For testing purposes
                var key hex = BitConverter.ToString(algorithm.Key).Replace("-", string.Empty);
                var iv hex = BitConverter.ToString(algorithm.IV).Replace("-", string.Empty);
                var enc b64 = Convert.ToBase64String(stream.ToArray());
                */
                return stream.ToArray();
            }
        }

        private AesCryptoServiceProvider GetCryptoProvider(byte[]? iv = null)
        {
            var provider = new AesCryptoServiceProvider
            {
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7,
                KeySize = 128,
                BlockSize = 128,
                Key = key
            };
            if (iv != null)
                provider.IV = iv;
            return provider;
        }

        public static byte[] GetEncryptionKey(string encryptionPassword)
        {
            using (var passwordGenerator = new Rfc2898DeriveBytes(encryptionPassword, PasswordPaddingSalt, PasswordSaltIterations))
            {
                return passwordGenerator.GetBytes(16);
            }
        }
    }
}