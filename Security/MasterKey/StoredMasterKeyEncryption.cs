using System;
using System.IO;
using Octopus.Client.Model;
using System.Security.Cryptography;
using System.Text;

namespace Octopus.Shared.Security.MasterKey
{
    public class StoredMasterKeyEncryption : IMasterKeyEncryption
    {
        readonly byte[] masterKey;
        readonly bool generateSalt;

        // Do not change these two values: will break export/import files
        static readonly byte[] passwordPaddingSalt = Encoding.UTF8.GetBytes("Octopuss");
        const int passwordSaltIterations = 1000;
        
        public StoredMasterKeyEncryption(byte[] masterKey)
        {
            this.masterKey = masterKey;
            this.generateSalt = true;
        }

        private StoredMasterKeyEncryption(byte[] masterKey, bool generateSalt)
        {
            this.masterKey = masterKey;
            this.generateSalt = generateSalt;
        }

        /// <summary>
        /// Creates a StoredMasterKeyEncryption from a password string of arbitrary length
        /// </summary>
        /// <remarks>
        /// The password is padded using Rfc2898 and a constant salt value
        /// </remarks>
        public static StoredMasterKeyEncryption FromPassword(string password)
        {
            // See http://stackoverflow.com/a/18096278/224370
            Rfc2898DeriveBytes pwdGen = new Rfc2898DeriveBytes(password, passwordPaddingSalt, passwordSaltIterations);
            var encryption = new StoredMasterKeyEncryption(pwdGen.GetBytes(16), false);
            return encryption;
        }

        public EncryptedBytes ToCiphertext(byte[] plaintext)
        {
            return MasterKeyEncryption.ToCiphertext(masterKey, plaintext, generateSalt);
        }

        public byte[] ToPlaintext(EncryptedBytes encrypted)
        {
            return MasterKeyEncryption.ToPlaintext(masterKey, encrypted);
        }

        public Stream ReadAsCiphertext(Stream plaintext)
        {
            return MasterKeyEncryption.ReadAsCiphertext(masterKey, plaintext);
        }

        public Stream ReadAsPlaintext(Stream ciphertext)
        {
            return MasterKeyEncryption.ReadAsPlaintext(masterKey, ciphertext);
        }

        public Stream WriteCiphertextTo(Stream stream)
        {
            return MasterKeyEncryption.WriteCiphertextTo(masterKey, stream);
        }
    }
}
