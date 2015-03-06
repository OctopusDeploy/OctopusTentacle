using System;
using System.IO;
using Octopus.Client.Model;

namespace Octopus.Shared.Security.MasterKey
{
    public class StoredMasterKeyEncryption : IMasterKeyEncryption
    {
        readonly byte[] masterKey;

        public StoredMasterKeyEncryption(byte[] masterKey)
        {
            this.masterKey = masterKey;
        }

        public EncryptedBytes ToCiphertext(byte[] plaintext)
        {
            return MasterKeyEncryption.ToCiphertext(masterKey, plaintext);
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
