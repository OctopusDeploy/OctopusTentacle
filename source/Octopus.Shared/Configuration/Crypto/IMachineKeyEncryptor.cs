using System;

namespace Octopus.Shared.Configuration.Crypto
{
    public interface IMachineKeyEncryptor
    {
        string Encrypt(string raw);
        string Decrypt(string encrypted);
    }
}