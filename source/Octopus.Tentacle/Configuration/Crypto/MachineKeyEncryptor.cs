using System;
using System.Collections.Generic;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Configuration.Crypto
{
    public class MachineKeyEncryptor : IMachineKeyEncryptor
    {
        public static readonly IMachineKeyEncryptor Current;

        static MachineKeyEncryptor()
        {
            if (PlatformDetection.IsRunningOnWindows)
            {
                Current = new WindowsMachineKeyEncryptor();
            }
            else
            {
                var log = new SystemLog();
                Current = CreateLinuxEncryptor(log, new OctopusPhysicalFileSystem(log));
            }
        }

        /// <summary>
        /// How the Linux encryptor is put together. Public so tests can prove the composition, not just the parts.
        /// </summary>
        public static IMachineKeyEncryptor CreateLinuxEncryptor(ISystemLog log, IOctopusFileSystem fileSystem)
        {
            // Everything is encrypted with the key Tentacle generates for this machine. Earlier versions preferred a
            // key taken straight from /etc/machine-id, which is neither secret nor unique inside container images,
            // and only fell back to the generated key when there was no machine-id; both are kept, in that order,
            // purely to decrypt what those versions wrote.
            var generatedKey = new LinuxGeneratedMachineKey(log, fileSystem);
            var legacyKeySources = new List<ICryptoKeyNixSource>
            {
                new LinuxMachineIdKey(fileSystem),
                generatedKey
            };

            // Those versions also kept the generated key at /etc/octopus/machinekey whatever the machine configuration
            // home was, so an install that relocated its home may have values written with a key that is still there.
            if (generatedKey.KeyFilePath != LinuxGeneratedMachineKey.StandardKeyFilePath)
                legacyKeySources.Add(new LinuxGeneratedMachineKey(log, fileSystem, LinuxGeneratedMachineKey.StandardKeyFilePath, createIfMissing: false));

            return new LinuxMachineKeyEncryptor(log, generatedKey, legacyKeySources);
        }

        MachineKeyEncryptor()
        {
        }

        public string Encrypt(string raw)
            => Current.Encrypt(raw);

        public string Decrypt(string encrypted)
            => Current.Decrypt(encrypted);

        public bool RequiresReEncryption(string encrypted)
            => Current.RequiresReEncryption(encrypted);
    }
}
