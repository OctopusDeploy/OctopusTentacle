using System;

namespace Octopus.Tentacle.Configuration.Crypto
{
    public class ConfigurationEncryptionKeyException : Exception
    {
        public ConfigurationEncryptionKeyException(string message)
            : base(message)
        {
        }

        public ConfigurationEncryptionKeyException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
