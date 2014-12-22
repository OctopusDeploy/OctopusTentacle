using System;
using System.IO;
using Octopus.Client.Model;

namespace Octopus.Shared.Security.MasterKey
{
    public class EncryptionUnsupported : IMasterKeyEncryption
    {
        public EncryptedBytes ToCiphertext(byte[] plaintext)
        {
            throw new NotSupportedException("Master key encryption is not supported in this context");
        }

        public byte[] ToPlaintext(EncryptedBytes encrypted)
        {
            throw new NotSupportedException("Master key encryption is not supported in this context");
        }

        public Stream ReadAsCiphertext(Stream plaintext)
        {
            throw new NotSupportedException("Master key encryption is not supported in this context");
        }

        public Stream ReadAsPlaintext(Stream ciphertext)
        {
            throw new NotSupportedException("Master key encryption is not supported in this context");
        }

        public Stream WriteCiphertextTo(Stream stream)
        {
            throw new NotSupportedException("Master key encryption is not supported in this context");
        }
    }
}