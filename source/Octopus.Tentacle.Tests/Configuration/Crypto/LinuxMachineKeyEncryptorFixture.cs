#if !NETFRAMEWORK
using System;
using System.Linq;
using System.Security.Cryptography;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Tests.Support;

namespace Octopus.Tentacle.Tests.Configuration.Crypto
{
    [TestFixture]
    public class LinuxMachineKeyEncryptorFixture
    {
        readonly ISystemLog systemLog = Substitute.For<ISystemLog>();
        readonly InMemoryCryptoKeyNixSource generatedKey = new();
        readonly InMemoryCryptoKeyNixSource legacyKey = new();

        LinuxMachineKeyEncryptor CreateEncryptor(params ICryptoKeyNixSource[] legacyKeySources)
            => new(systemLog, generatedKey, legacyKeySources);

        [Test]
        public void EncryptsWithTheVersionPrefixAndDecrypts()
        {
            var encryptor = CreateEncryptor();

            var encrypted = encryptor.Encrypt("FooBar");

            encrypted.Should().StartWith(LinuxMachineKeyEncryptor.ProtectedValuePrefix);
            encryptor.RequiresReEncryption(encrypted).Should().BeFalse();
            encryptor.Decrypt(encrypted).Should().Be("FooBar");
        }

        [Test]
        public void ChangingAnyByteOfACurrentValueIsDetected()
        {
            var encryptor = CreateEncryptor();
            var encrypted = encryptor.Encrypt("FooBar, but long enough to span more than one block of AES");
            var payload = Convert.FromBase64String(encrypted.Substring(LinuxMachineKeyEncryptor.ProtectedValuePrefix.Length));

            for (var i = 0; i < payload.Length; i++)
            {
                var tampered = payload.ToArray();
                tampered[i] ^= 0x01;
                var value = LinuxMachineKeyEncryptor.ProtectedValuePrefix + Convert.ToBase64String(tampered);

                encryptor.Invoking(x => x.Decrypt(value)).Should().Throw<CryptographicException>($"byte {i} was changed");
            }
        }

        [Test]
        public void ACurrentValueEncryptedWithAnotherKeyIsNeverDecryptedWithALegacyKey()
        {
            var otherKey = new InMemoryCryptoKeyNixSource();
            var encryptedWithOtherKey = new LinuxMachineKeyEncryptor(systemLog, otherKey, Array.Empty<ICryptoKeyNixSource>()).Encrypt("FooBar");

            CreateEncryptor(otherKey).Invoking(x => x.Decrypt(encryptedWithOtherKey)).Should().Throw<CryptographicException>();
        }

        [Test]
        public void LegacyValuesAreDecryptedWithTheFirstLegacyKeyThatWorks()
        {
            var brokenKey = Substitute.For<ICryptoKeyNixSource>();
            brokenKey.Load().Returns((new byte[] { 77 }, new byte[] { 43, 11 }));
            var legacy = LegacySchemeEncryption.Encrypt(legacyKey, "FooBar");

            var encryptor = CreateEncryptor(brokenKey, legacyKey);

            encryptor.RequiresReEncryption(legacy).Should().BeTrue();
            encryptor.Decrypt(legacy).Should().Be("FooBar");
        }
    }
}
#endif
