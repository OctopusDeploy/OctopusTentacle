using System;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class MachineKeyEncrypter : IMachineKeyEncryptor
    {
        public static readonly IMachineKeyEncryptor Current;

        static MachineKeyEncrypter()
        {
            Current = PlatformDetection.IsRunningOnWindows ? new WindowsMachineKeyEncryptor() : LinuxEncryptor();
        }

        static IMachineKeyEncryptor LinuxEncryptor()
        {
            var log = new SystemLog();
            // Sources to find the crypto IV+Key. We want to enforce trying to use the machine-key
            // first but still fallback to the existing Octopus generated one if that doesnt work.
            var keySources = new LinuxMachineKeyEncryptor.ICryptoKeyNixSource[]
            {
                new LinuxMachineKeyEncryptor.LinuxMachineIdKey(log), new LinuxMachineKeyEncryptor.LinuxGeneratedMachineKey(log)
            };

            return new LinuxMachineKeyEncryptor(keySources);

        }

        MachineKeyEncrypter()
        {
        }

        public string Encrypt(string raw)
            => Current.Encrypt(raw);

        public string Decrypt(string encrypted)
            => Current.Decrypt(encrypted);
    }
}