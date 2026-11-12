namespace Octopus.Tentacle.Configuration
{
    public interface IReEncryptingKeyValueStore
    {
        // Throws, leaving the stored value untouched, if it cannot be decrypted, re-encrypted or saved.
        bool ReEncryptIfLegacy(string name);

        void RestrictKeyStorageToOwner();
    }
}
