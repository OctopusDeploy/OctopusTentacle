using System;
using System.IO;
using System.Text;
using Octopus.Client.Model;

namespace Octopus.Shared.Security.MasterKey
{
    public static class MasterKeyEncryptionExtensions
    {
        public static EncryptedBytes StringToCiphertext(this IMasterKeyEncryption encryption, string plaintext)
        {
            if (encryption == null) throw new ArgumentNullException("encryption");
            var bytes = Encoding.UTF8.GetBytes(plaintext);
            return encryption.ToCiphertext(bytes);
        }

        public static string ToPlaintextString(this IMasterKeyEncryption encryption, EncryptedBytes encrypted)
        {
            if (encryption == null) throw new ArgumentNullException("encryption");
            if (encrypted == null) throw new ArgumentNullException("encrypted");
            var bytes = encryption.ToPlaintext(encrypted);
            return Encoding.UTF8.GetString(bytes);
        }

        public static byte[] ToDecryptedBytes(this IMasterKeyEncryption encryption, string encryptedBase64String)
        {
            using (var encryptedStream = new MemoryStream(Convert.FromBase64String(encryptedBase64String)))
            {
                using (var plainTextStream = encryption.ReadAsPlaintext(encryptedStream))
                {
                    var bytes = ReadFully(plainTextStream);
                    return bytes;
                }
            }
        }

        static byte[] ReadFully(Stream input)
        {
            var buffer = new byte[16 * 1024];
            using (var ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }
    }
}