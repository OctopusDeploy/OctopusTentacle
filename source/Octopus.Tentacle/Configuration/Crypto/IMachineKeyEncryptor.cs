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
    }
}
