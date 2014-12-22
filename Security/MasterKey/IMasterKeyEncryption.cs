using System;
using System.IO;
using Octopus.Platform.Model;

namespace Octopus.Platform.Security.MasterKey
{
    public interface IMasterKeyEncryption
    {
        EncryptedBytes ToCiphertext(byte[] plaintext);
        byte[] ToPlaintext(EncryptedBytes encrypted);
        Stream ReadAsCiphertext(Stream plaintext);
        Stream ReadAsPlaintext(Stream ciphertext);
        Stream WriteCiphertextTo(Stream stream);
    }
}
