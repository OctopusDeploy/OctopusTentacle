using System;
using System.Security.Cryptography;
using NSubstitute;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Diagnostics;
using PlatformDetection = Octopus.Tentacle.Util.PlatformDetection;

namespace Octopus.Tentacle.Tests.Support
{
    // On Linux, production generates a key beside the configuration file, which tests must neither depend on nor create;
    // an in-memory key stands in for it.
    public static class TestMachineKeyEncryptor
    {
        public static readonly IMachineKeyEncryptor Current = PlatformDetection.IsRunningOnWindows
            ? new WindowsMachineKeyEncryptor()
            : new LinuxMachineKeyEncryptor(Substitute.For<ISystemLog>(), new InMemoryCryptoKeyNixSource(), Array.Empty<ICryptoKeyNixSource>());
    }

    public class InMemoryCryptoKeyNixSource : ICryptoKeyNixSource
    {
        public InMemoryCryptoKeyNixSource()
        {
            using var aes = Aes.Create();
            aes.GenerateIV();
            aes.GenerateKey();
            Key = aes.Key;
            IV = aes.IV;
        }

        public byte[] Key { get; }
        public byte[] IV { get; }

        public (byte[] Key, byte[] IV) Load()
        {
            return (Key, IV);
        }
    }
}
