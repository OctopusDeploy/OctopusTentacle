using System;

namespace Octopus.Shared.Configuration
{
    public interface IMachineKeyEncryptor
    {
        string Encrypt(string raw);
        string Decrypt(string encrypted);
    }
}