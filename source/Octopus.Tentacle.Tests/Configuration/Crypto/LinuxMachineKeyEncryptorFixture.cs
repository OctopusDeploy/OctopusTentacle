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
        readonly ConfigurationEncryptionKey generatedKey = InMemoryConfigurationEncryptionKey.Create();

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
            encrypted.Should().MatchRegex(@"^AES256-GCM\$[A-Za-z0-9+/=]+\$[A-Za-z0-9+/=]+$");
            encryptor.RequiresReEncryption(encrypted).Should().BeFalse();
            encryptor.Decrypt(encrypted).Should().Be("FooBar");
        }

        [Test]
        public void WhenACurrentValueWasEncryptedWithAnotherKeyThenALegacyKeyIsNotTried()
        {
            // Arrange
            var otherKey = InMemoryConfigurationEncryptionKey.Create();
            var encryptedWithOtherKey = new LinuxMachineKeyEncryptor(systemLog, otherKey, Array.Empty<ICryptoKeyNixSource>()).Encrypt("FooBar");
            var encryptor = CreateEncryptor(new InMemoryCryptoKeyNixSource());

            // Act
            Action decrypt = () => encryptor.Decrypt(encryptedWithOtherKey);

            // Assert
            decrypt.Should().Throw<CryptographicException>();
        }

        [TestCase("AES128-GCM")]
        [TestCase("aes256-gcm")]
        public void WhenTheSpecifierIsAlteredThenDecryptionFails(string alteredSpecifier)
        {
            // Arrange
            var encryptor = CreateEncryptor();
            var encrypted = encryptor.Encrypt("FooBar");
            var tampered = alteredSpecifier + encrypted.Substring(LinuxMachineKeyEncryptor.Specifier.Length);

            // Act
            Action decrypt = () => encryptor.Decrypt(tampered);

            // Assert
            decrypt.Should().Throw<CryptographicException>().WithMessage("*Unknown encryption specifier*");
        }
    }
}
#endif
