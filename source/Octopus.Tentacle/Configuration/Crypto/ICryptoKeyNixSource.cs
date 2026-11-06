using System;

namespace Octopus.Tentacle.Configuration.Crypto
{
    public interface ICryptoKeyNixSource
    {
        (byte[] Key, byte[] IV) Load();

        // Null when the key does not exist. Never creates it: decrypting with a key that did not exist cannot succeed, and
        // creating one would leave a useless key in the way of the real one being restored.
        (byte[] Key, byte[] IV)? TryLoad();
    }
}