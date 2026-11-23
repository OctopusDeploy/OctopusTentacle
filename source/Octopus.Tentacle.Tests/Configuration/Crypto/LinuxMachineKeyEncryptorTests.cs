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
    public class LinuxMachineKeyEncryptorTests
    {
        [Test]
        public void WhenAValueIsEncryptedThenItIsVersionedAndDecrypts()
        {
            // Arrange
            var encryptor = new LinuxMachineKeyEncryptor(Substitute.For<ISystemLog>(), InMemoryConfigurationEncryptionKey.Create(), Array.Empty<ICryptoKeyNixSource>());

            // Act
            var encrypted = encryptor.Encrypt("FooBar");

            // Assert
            encrypted.Should().MatchRegex(@"^AESGCM-V1\$[A-Za-z0-9+/=]{16}\$[A-Za-z0-9+/=]{24}\$[A-Za-z0-9+/=]+$");
            encryptor.RequiresReEncryption(encrypted).Should().BeFalse();
            encryptor.Decrypt(encrypted).Should().Be("FooBar");
        }

        [Test]
        public void WhenACurrentValueWasEncryptedWithAnotherKeyThenALegacyKeyIsNotTried()
        {
            // Arrange
            var encryptedWithOtherKey = new LinuxMachineKeyEncryptor(Substitute.For<ISystemLog>(), InMemoryConfigurationEncryptionKey.Create(), Array.Empty<ICryptoKeyNixSource>()).Encrypt("FooBar");
            var encryptor = new LinuxMachineKeyEncryptor(Substitute.For<ISystemLog>(), InMemoryConfigurationEncryptionKey.Create(), new ICryptoKeyNixSource[] { new InMemoryCryptoKeyNixSource() });

            // Act
            Action decrypt = () => encryptor.Decrypt(encryptedWithOtherKey);

            // Assert
            decrypt.Should().Throw<CryptographicException>();
        }

        [TestCase("AESGCM-V2")]
        [TestCase("aesgcm-v1")]
        public void WhenTheSpecifierIsAlteredThenDecryptionFails(string alteredSpecifier)
        {
            // Arrange
            var encryptor = new LinuxMachineKeyEncryptor(Substitute.For<ISystemLog>(), InMemoryConfigurationEncryptionKey.Create(), Array.Empty<ICryptoKeyNixSource>());
            var tampered = alteredSpecifier + encryptor.Encrypt("FooBar").Substring(LinuxMachineKeyEncryptor.Specifier.Length);

            // Act
            Action decrypt = () => encryptor.Decrypt(tampered);

            // Assert
            decrypt.Should().Throw<CryptographicException>().WithMessage("*Unknown encryption specifier*");
        }
    }
}
#endif
