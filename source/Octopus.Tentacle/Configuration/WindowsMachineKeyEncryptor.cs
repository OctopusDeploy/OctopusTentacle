using System;
using System.Security.Cryptography;
using System.Text;
using Octopus.Tentacle.Configuration.Crypto;

namespace Octopus.Tentacle.Configuration
{
    public class WindowsMachineKeyEncryptor : IMachineKeyEncryptor
    {
        public string Encrypt(string raw)
            => Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(raw), null, DataProtectionScope.LocalMachine));

        public string Decrypt(string encrypted)
            => Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(encrypted), null, DataProtectionScope.LocalMachine));
    }
}