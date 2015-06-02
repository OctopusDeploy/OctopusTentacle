using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Octopus.Client.Model;

namespace Octopus.Shared.Security.MasterKey
{
    public class StoredMasterKeyEncryption : IMasterKeyEncryption
    {
        const int passwordSaltIterations = 1000;
        // Do not change these two values: will break export/import files
        static readonly byte[] passwordPaddingSalt = Encoding.UTF8.GetBytes("Octopuss");
        readonly byte[] masterKey;
        readonly bool generateSalt;

        public StoredMasterKeyEncryption(byte[] masterKey)
        {
            this.masterKey = masterKey;
            generateSalt = true;
        }

        StoredMasterKeyEncryption(byte[] masterKey, bool generateSalt)
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
            var pwdGen = new Rfc2898DeriveBytes(password, passwordPaddingSalt, passwordSaltIterations);
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