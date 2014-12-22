using System;
using System.IO;
using Octopus.Platform.Model;

namespace Octopus.Platform.Security.MasterKey
{
    public class WriteOnlyEncryption : IMasterKeyEncryption
    {
        readonly IMasterKeyEncryption encryption;

        public WriteOnlyEncryption(IMasterKeyEncryption encryption)
        {
            this.encryption = encryption;
        }

        public EncryptedBytes ToCiphertext(byte[] plaintext)
        {
            return encryption.ToCiphertext(plaintext);
        }

        public byte[] ToPlaintext(EncryptedBytes encrypted)
        {
            return new byte[0];
        }

        public Stream ReadAsCiphertext(Stream plaintext)
        {
            return encryption.ReadAsCiphertext(plaintext);
        }

        public Stream ReadAsPlaintext(Stream ciphertext)
        {
            return new MemoryStream();
        }

        public Stream WriteCiphertextTo(Stream stream)
        {
            return encryption.WriteCiphertextTo(stream);
        }
    }
}
