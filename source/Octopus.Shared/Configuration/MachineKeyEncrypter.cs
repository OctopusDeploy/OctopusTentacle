using System;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class MachineKeyEncrypter: IMachineKeyEncryptor
    {
        static MachineKeyEncrypter()
        {
            Current = PlatformDetection.IsRunningOnWindows ?
                (IMachineKeyEncryptor)new WindowsMachineKeyEncryptor() :
                new LinuxMachineKeyEncryptor();
        }

        public static readonly IMachineKeyEncryptor Current;

        private MachineKeyEncrypter() { }
        
        public string Encrypt(string raw)
        {
            return Current.Encrypt(raw);
        }

        public string Decrypt(string encrypted)
        {
            return Current.Decrypt(encrypted);
        }
    }
}