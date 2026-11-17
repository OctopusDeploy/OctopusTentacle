using System;

namespace Octopus.Tentacle.Configuration.Crypto
{
    public interface ICryptoKeyNixSource
    {
        (byte[] Key, byte[] IV) Load();
    }
}