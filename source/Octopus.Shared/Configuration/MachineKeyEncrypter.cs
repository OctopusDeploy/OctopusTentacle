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
            Current = PlatformDetection.IsRunningOnWindows ? (IMachineKeyEncryptor)new WindowsMachineKeyEncryptor() : new LinuxMachineKeyEncryptor(new SystemLog());
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