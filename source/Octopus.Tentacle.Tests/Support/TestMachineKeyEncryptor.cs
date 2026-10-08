using System;
using System.Security.Cryptography;
using NSubstitute;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Diagnostics;
using PlatformDetection = Octopus.Tentacle.Util.PlatformDetection;

namespace Octopus.Tentacle.Tests.Support
{
    /// <summary>
    /// The encryptor tests hand to key-value stores that hold <see cref="Octopus.Tentacle.Configuration.ProtectionLevel.MachineKey"/> values.
    ///
    /// On Windows it is the real DPAPI encryptor, exactly as production uses it. Elsewhere production generates a key
    /// into /etc/octopus/machinekey, which tests must neither depend on nor create, so an otherwise identical Linux
    /// encryptor backed by an in-memory key is used instead.
    /// </summary>
    public static class TestMachineKeyEncryptor
    {
        public static readonly IMachineKeyEncryptor Current = PlatformDetection.IsRunningOnWindows
            ? MachineKeyEncryptor.Current
            : new LinuxMachineKeyEncryptor(Substitute.For<ISystemLog>(), new InMemoryCryptoKeyNixSource(), Array.Empty<ICryptoKeyNixSource>());
    }

    /// <summary>
    /// A freshly generated AES key and IV that never touch the disk.
    /// </summary>
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
