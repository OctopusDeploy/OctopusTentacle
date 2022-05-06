using System;

namespace Octopus.Shared.Configuration.Crypto
{
    public interface ICryptoKeyNixSource
    {
        (byte[] Key, byte[] IV) Load();
    }
}