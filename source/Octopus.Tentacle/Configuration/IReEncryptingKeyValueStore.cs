namespace Octopus.Tentacle.Configuration
{
    /// <summary>
    /// A key-value store that can rewrite a <see cref="ProtectionLevel.MachineKey"/> value in place with the current
    /// encryption scheme when it was written by a superseded one.
    /// </summary>
    public interface IReEncryptingKeyValueStore
    {
        /// <summary>
        /// Re-encrypts the named setting if, and only if, its stored form was produced by a superseded encryption
        /// scheme (see <see cref="Crypto.IMachineKeyEncryptor.RequiresReEncryption"/>). Returns true if the value was
        /// rewritten, false if there was nothing to do. Throws if the value cannot be decrypted, re-encrypted or saved,
        /// in which case the stored value is left untouched.
        /// </summary>
        bool ReEncryptIfLegacy(string name);
    }
}
