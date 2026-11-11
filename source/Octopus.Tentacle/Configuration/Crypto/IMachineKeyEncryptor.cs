using System;

namespace Octopus.Tentacle.Configuration.Crypto
{
    public interface IMachineKeyEncryptor
    {
        string Encrypt(string raw);
        string Decrypt(string encrypted);

        bool RequiresReEncryption(string encrypted);

        // Only the agent calls this, as the user the service runs as; see LinuxGeneratedMachineKey.RestrictPermissionsToOwner.
        void RestrictKeyStorageToOwner();
    }
}
