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

        public static IMachineKeyEncryptor ForConfigurationFile(IOctopusFileSystem fileSystem, string configurationFile)
            => PlatformDetection.IsRunningOnWindows
                ? new WindowsMachineKeyEncryptor()
                : CreateLinuxEncryptor(Log, fileSystem, LinuxGeneratedMachineKey.KeyFilePathFor(configurationFile), configurationFile);

        internal static IMachineKeyEncryptor CreateLinuxEncryptor(ISystemLog log, IOctopusFileSystem fileSystem, string keyFilePath, string? configurationFile)
        {
            var generatedKey = new LinuxGeneratedMachineKey(log, fileSystem, keyFilePath, createIfMissing: true, ownerReferencePath: configurationFile);

            // The keys earlier versions used, in the order they tried them. Only read, never created or changed.
            var legacyKeySources = new List<ICryptoKeyNixSource>
            {
                new LinuxMachineIdKey(fileSystem),
                new LinuxGeneratedMachineKey(log, fileSystem, LinuxGeneratedMachineKey.LegacyKeyFilePathForThisHost, createIfMissing: false)
            };

            return new LinuxMachineKeyEncryptor(log, generatedKey, legacyKeySources);
        }
    }
}
