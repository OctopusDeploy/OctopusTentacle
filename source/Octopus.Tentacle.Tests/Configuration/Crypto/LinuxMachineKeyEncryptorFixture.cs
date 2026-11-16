#if !NETFRAMEWORK
using System;
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

        LinuxMachineKeyEncryptor CreateEncryptor(params ICryptoKeyNixSource[] legacyKeySources)
            => new(systemLog, generatedKey, legacyKeySources);

        [Test]
        public void WhenAValueIsEncryptedThenItIsVersionedAndDecrypts()
        {
            // Arrange
            var encryptor = CreateEncryptor();

            // Act
            var encrypted = encryptor.Encrypt("FooBar");

            // Assert
            encrypted.Should().StartWith(LinuxMachineKeyEncryptor.ProtectedValuePrefix);
            encryptor.RequiresReEncryption(encrypted).Should().BeFalse();
            encryptor.Decrypt(encrypted).Should().Be("FooBar");
        }

        [Test]
        public void WhenACurrentValueWasEncryptedWithAnotherKeyThenALegacyKeyIsNotTried()
        {
            // Arrange
            var otherKey = new InMemoryCryptoKeyNixSource();
            var encryptedWithOtherKey = new LinuxMachineKeyEncryptor(systemLog, otherKey, Array.Empty<ICryptoKeyNixSource>()).Encrypt("FooBar");
            var encryptor = CreateEncryptor(otherKey);

            // Act
            Action decrypt = () => encryptor.Decrypt(encryptedWithOtherKey);

            // Assert
            decrypt.Should().Throw<CryptographicException>();
        }
    }
}
#endif
