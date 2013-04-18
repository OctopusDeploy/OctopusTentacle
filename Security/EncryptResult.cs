using System;
using System.Text;

namespace Octopus.Shared.Security
{
    public class EncryptResult
    {
        private readonly byte[] cipherText;
        private readonly byte[] salt;

        public EncryptResult(byte[] cipherText, byte[] salt)
        {
            this.cipherText = cipherText;
            this.salt = salt;
        }

        public byte[] CipherText
        {
            get { return cipherText; }
        }

        public byte[] Salt
        {
            get { return salt; }
        }

        public string ToBase64()
        {
            var cipher64 = Convert.ToBase64String(cipherText);
            var salt64 = Convert.ToBase64String(salt);

            return cipher64 + "|" + salt64;
        }

        public static EncryptResult FromBase64(string base64)
        {
            var parts = base64.Split('|');
            var cipher = Convert.FromBase64String(parts[0]);
            var salt = Convert.FromBase64String(parts[1]);
            return new EncryptResult(cipher, salt);
        }
    }
}