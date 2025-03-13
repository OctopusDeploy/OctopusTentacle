using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Octopus.Tentacle.Security
{
    public class AesEncryption
    {
        const int PasswordSaltIterations = 1000;
        static readonly byte[] PasswordPaddingSalt = Encoding.UTF8.GetBytes("Octopuss");
        static readonly byte[] IvPrefix = Encoding.UTF8.GetBytes("IV__");

        readonly byte[] key;

        public AesEncryption(string password)
        {
            key = GetEncryptionKey(password);
        }

        public string Decrypt(byte[] encrypted)
        {
            using var ms = new MemoryStream(encrypted);
            var iv = new byte[16];
            ms.Position = IvPrefix.Length;
            _ = ms.Read(iv, 0, iv.Length);

            using var algorithm = GetCryptoProvider(iv);
            using var dec = algorithm.CreateDecryptor();
            using var cs = new CryptoStream(ms, dec, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs, Encoding.UTF8);
            return sr.ReadToEnd();
        }

        public byte[] Encrypt(string plaintext)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plaintext);

            using var algorithm = GetCryptoProvider();
            using var encryptor = algorithm.CreateEncryptor();
            using var stream = new MemoryStream();
            // The IV is randomly generated each time so safe to append
            stream.Write(IvPrefix, 0, IvPrefix.Length);
            stream.Write(algorithm.IV, 0, algorithm.IV.Length);
            
            using (var cStream = new CryptoStream(stream, encryptor, CryptoStreamMode.Write))
            {
                cStream.Write(plainTextBytes, 0, plainTextBytes.Length);
            }

            // For testing purposes
            // var key hex = BitConverter.ToString(algorithm.Key).Replace("-", string.Empty);
            // var iv hex = BitConverter.ToString(algorithm.IV).Replace("-", string.Empty);
            // var enc b64 = Convert.ToBase64String(stream.ToArray());
            return stream.ToArray();
        }

        Aes GetCryptoProvider(byte[]? iv = null)
        {
            var provider = Aes.Create();
            provider.Mode = CipherMode.CBC;
            provider.Padding = PaddingMode.PKCS7;
            provider.KeySize = 256;
            provider.BlockSize = 128;
            provider.Key = key;

            if (iv != null)
            {
                provider.IV = iv;
            }

            return provider;
        }

        static byte[] GetEncryptionKey(string encryptionPassword)
        {
// NET8 requires explicit encryption algorithm specified as the other overload method has been marked as obsolete. The default encryption value is SHA1.
#if NET8_0_OR_GREATER
            using var passwordGenerator = new Rfc2898DeriveBytes(encryptionPassword, PasswordPaddingSalt, PasswordSaltIterations, HashAlgorithmName.SHA1);
#else
            using var passwordGenerator = new Rfc2898DeriveBytes(encryptionPassword, PasswordPaddingSalt, PasswordSaltIterations);
#endif
            return passwordGenerator.GetBytes(16);
        }
    }
}