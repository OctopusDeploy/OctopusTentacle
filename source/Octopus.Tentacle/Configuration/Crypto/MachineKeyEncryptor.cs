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

        /// <summary>
        /// The encryptor for the values in the configuration file at <paramref name="configurationFile"/>. On Linux its
        /// key is <c>config-encryption.key</c> beside that file (see <see cref="LinuxGeneratedMachineKey.KeyFilePathFor"/>),
        /// so there is no host-wide key: every protected value belongs to a configuration file. On Windows it is DPAPI,
        /// as it always was.
        /// </summary>
        public static IMachineKeyEncryptor ForConfigurationFile(string configurationFile)
            => PlatformDetection.IsRunningOnWindows
                ? new WindowsMachineKeyEncryptor()
                : CreateLinuxEncryptor(Log, new OctopusPhysicalFileSystem(Log), LinuxGeneratedMachineKey.KeyFilePathFor(configurationFile), configurationFile);

        /// <summary>
        /// How the Linux encryptor is put together. Public so tests can prove the composition, not just the parts.
        /// </summary>
        /// <param name="keyFilePath">Where the key for new values is kept, and created if need be.</param>
        /// <param name="configurationFile">The configuration file the key protects, if any; a key root creates is given its owner.</param>
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
