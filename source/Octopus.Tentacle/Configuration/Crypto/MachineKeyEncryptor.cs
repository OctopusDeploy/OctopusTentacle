using System;
using System.Collections.Generic;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Configuration.Crypto
{
    public static class MachineKeyEncryptor
    {
        static readonly ISystemLog Log = new SystemLog();

        public static IMachineKeyEncryptor ForKeyFile(IOctopusFileSystem fileSystem, string? keyFilePath)
            => PlatformDetection.IsRunningOnWindows
                ? new WindowsMachineKeyEncryptor()
                : keyFilePath == null
                    ? CreateLinuxEncryptor(Log, fileSystem, new NoConfigurationEncryptionKey())
                    : CreateLinuxEncryptor(Log, fileSystem, keyFilePath);

        internal static IMachineKeyEncryptor CreateLinuxEncryptor(ISystemLog log, IOctopusFileSystem fileSystem, string keyFilePath)
            => CreateLinuxEncryptor(log, fileSystem, new LinuxGeneratedMachineKey(log, fileSystem, keyFilePath));

        static IMachineKeyEncryptor CreateLinuxEncryptor(ISystemLog log, IOctopusFileSystem fileSystem, ICryptoKeyNixSource generatedKey)
        {
            // The keys earlier versions used, in the order they tried them. Only read, never created or changed.
            var legacyKeySources = new List<ICryptoKeyNixSource>
            {
                new LinuxMachineIdKey(fileSystem),
                new LinuxGeneratedMachineKey(log, fileSystem, LinuxGeneratedMachineKey.LegacyKeyFilePathForThisHost)
            };

            return new LinuxMachineKeyEncryptor(log, generatedKey, legacyKeySources);
        }

        class NoConfigurationEncryptionKey : ICryptoKeyNixSource
        {
            public (byte[] Key, byte[] IV) Load()
                => throw new InvalidOperationException("The configuration encryption key for this configuration is not available. Register the configuration with `create-instance --instance <name>`.");

            public (byte[] Key, byte[] IV)? TryLoad() => null;
        }
    }
}
