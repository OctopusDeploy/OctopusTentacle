using System;
using System.IO;
using Octopus.Client.Model;

namespace Octopus.Shared.Security.MasterKey
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