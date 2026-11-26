using System;

namespace Octopus.Tentacle.Configuration.Crypto
{
    public class ProtectedSettingDecryptionException : Exception
    {
        public ProtectedSettingDecryptionException(string settingName, Exception inner)
            // Include inner message so it shows when Tentacle is run on the command line.
            : base($"Unable to decrypt the protected configuration setting '{settingName}'. {inner.Message}", inner)
        {
            SettingName = settingName;
        }

        public string SettingName { get; }
    }
}
