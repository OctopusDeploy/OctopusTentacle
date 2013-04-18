using System;

namespace Octopus.Shared.Security
{
    public abstract class EncryptionAlgorithm
    {
        public abstract EncryptResult Encrypt(string plainText);
        public abstract string Decrypt(byte[] cipherText, byte[] salt);
    }
}