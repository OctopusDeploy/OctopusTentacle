using System;

namespace Octopus.Tentacle.Configuration.Crypto
{
    public interface IMachineKeyEncryptor
    {
        string Encrypt(string raw);
        string Decrypt(string encrypted);
    }
}