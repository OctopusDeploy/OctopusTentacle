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

        // There is deliberately no host-wide key: every protected value belongs to a configuration file.
        public static IMachineKeyEncryptor ForConfigurationFile(string configurationFile)
            => PlatformDetection.IsRunningOnWindows
                ? new WindowsMachineKeyEncryptor()
                : CreateLinuxEncryptor(Log, new OctopusPhysicalFileSystem(Log), LinuxGeneratedMachineKey.KeyFilePathFor(configurationFile), configurationFile);

        // Public so tests can prove the composition, not just the parts. A key that root creates is given the owner of
        // configurationFile, when there is one.
        public static IMachineKeyEncryptor CreateLinuxEncryptor(ISystemLog log, IOctopusFileSystem fileSystem, string keyFilePath, string? configurationFile)
        {
            var generatedKey = new LinuxGeneratedMachineKey(log, fileSystem, keyFilePath, createIfMissing: true, ownerReferencePath: configurationFile);

            // Earlier versions preferred a key taken straight from /etc/machine-id, which is neither secret nor unique
            // inside container images, and fell back to a key they generated at /etc/octopus/machinekey (whatever the
            // configuration file's location) when there was no machine-id. Both are kept, in that order, purely to
            // decrypt what those versions wrote. Neither is ever created or changed: a missing legacy key cannot decrypt
            // anything, and leaving the old key file alone keeps a pre-upgrade backup of the configuration readable.
            var legacyKeySources = new List<ICryptoKeyNixSource>
            {
                new LinuxMachineIdKey(fileSystem),
                new LinuxGeneratedMachineKey(log, fileSystem, LinuxGeneratedMachineKey.LegacyKeyFilePathForThisHost, createIfMissing: false)
            };

            return new LinuxMachineKeyEncryptor(log, generatedKey, legacyKeySources);
        }
    }
}
