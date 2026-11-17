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
                : CreateLinuxEncryptor(Log, fileSystem, keyFilePath);

        internal static IMachineKeyEncryptor CreateLinuxEncryptor(ISystemLog log, IOctopusFileSystem fileSystem, string? keyFilePath)
        {
            // The keys earlier versions used, in the order they tried them. Only read, never created or changed.
            var legacyKeySources = new List<ICryptoKeyNixSource>
            {
                new LinuxMachineIdKey(fileSystem),
                new LinuxGeneratedMachineKey(fileSystem)
            };

            return new LinuxMachineKeyEncryptor(log, new ConfigurationEncryptionKey(log, fileSystem, keyFilePath), legacyKeySources);
        }
    }
}
