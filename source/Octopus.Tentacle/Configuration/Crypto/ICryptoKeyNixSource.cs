using System;

namespace Octopus.Tentacle.Configuration.Crypto
{
    public interface ICryptoKeyNixSource
    {
        (byte[] Key, byte[] IV) Load();

        // Never creates the key.
        (byte[] Key, byte[] IV)? TryLoad();
    }
}