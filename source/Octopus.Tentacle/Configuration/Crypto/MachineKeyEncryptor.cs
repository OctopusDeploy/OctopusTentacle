using System;
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
            if (KubernetesConfig.IsRunningInKubernetesCluster)
            {
                Current = new KubernetesMachineKeyEncryptor(new SystemLog());
            }
            else if (PlatformDetection.IsRunningOnWindows)
            {
                Current = new WindowsMachineKeyEncryptor();
            }
            else
            {
                Current = LinuxEncryptor();
            }
        }

        static IMachineKeyEncryptor LinuxEncryptor()
        {
            var log = new SystemLog();
            var filesystem = new OctopusPhysicalFileSystem(log);
            // Sources to find the crypto IV+Key. We want to enforce trying to use the machine-key
            // first but still fallback to the existing Octopus generated one if that doesnt work.
            var keySources = new ICryptoKeyNixSource[]
            {
                new LinuxMachineIdKey(filesystem),
                new LinuxGeneratedMachineKey(log, filesystem)
            };

            return new LinuxMachineKeyEncryptor(log, keySources);
        }

        MachineKeyEncryptor()
        {
        }

        public string Encrypt(string raw)
            => Current.Encrypt(raw);

        public string Decrypt(string encrypted)
            => Current.Decrypt(encrypted);
    }
}