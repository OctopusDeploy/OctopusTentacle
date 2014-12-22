using System;
using System.Text;
using Octopus.Client.Model;
using Octopus.Platform.Model;

namespace Octopus.Platform.Security.MasterKey
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
    }
}