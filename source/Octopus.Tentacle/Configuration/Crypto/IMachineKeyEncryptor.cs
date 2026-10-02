using System;

namespace Octopus.Tentacle.Configuration.Crypto
{
    public interface IMachineKeyEncryptor
    {
        string Encrypt(string raw);
        string Decrypt(string encrypted);

        /// <summary>
        /// Returns true when <paramref name="encrypted"/> was produced by a superseded encryption scheme that this
        /// encryptor can still decrypt, so that the value should be re-encrypted with <see cref="Encrypt"/> at the
        /// next opportunity and the old scheme retired.
        /// </summary>
        bool RequiresReEncryption(string encrypted);

        /// <summary>
        /// Makes sure the stored key, if this encryptor has one on disk, is readable only by its owner. Called only by
        /// the agent, as the user the service runs as; see <see cref="LinuxGeneratedMachineKey.RestrictPermissionsToOwner"/>.
        /// </summary>
        void RestrictKeyStorageToOwner();
    }
}
