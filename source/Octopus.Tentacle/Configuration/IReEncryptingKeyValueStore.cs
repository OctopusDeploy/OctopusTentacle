namespace Octopus.Tentacle.Configuration
{
    public interface IReEncryptingKeyValueStore
    {
        // Returns true if the value was rewritten. Throws if it cannot be decrypted, re-encrypted or saved, in which case the
        // stored value is left untouched.
        bool ReEncryptIfLegacy(string name);

        // A file-backed store also tightens the file it keeps the protected values in. Never throws.
        void RestrictKeyStorageToOwner();
    }
}
