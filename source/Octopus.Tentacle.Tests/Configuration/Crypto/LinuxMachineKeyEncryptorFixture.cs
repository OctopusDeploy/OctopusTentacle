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
        InMemoryCryptoKeyNixSource generatedKey;
        InMemoryCryptoKeyNixSource legacyKey;

        [SetUp]
        public void SetUp()
        {
            generatedKey = new InMemoryCryptoKeyNixSource();
            legacyKey = new InMemoryCryptoKeyNixSource();
        }

        LinuxMachineKeyEncryptor CreateEncryptor(params ICryptoKeyNixSource[] legacyKeySources)
            => new LinuxMachineKeyEncryptor(systemLog, generatedKey, legacyKeySources);

        [Test]
        public void GivenValidKeysAvailable_ThenEncryptsAndDecrypts()
        {
            var lme = CreateEncryptor();

            var encrypted = lme.Encrypt("FooBar");
            var decrypted = lme.Decrypt(encrypted);

            encrypted.Should().NotBe("FooBar");
            decrypted.Should().Be("FooBar");
        }

        [Test]
        public void EmptyStringRoundTrips()
        {
            var lme = CreateEncryptor();

            lme.Decrypt(lme.Encrypt(string.Empty)).Should().BeEmpty();
        }

        [Test]
        public void EncryptedValuesCarryTheVersionPrefix()
        {
            var encrypted = CreateEncryptor().Encrypt("FooBar");

            encrypted.Should().StartWith(LinuxMachineKeyEncryptor.ProtectedValuePrefix);
            LinuxMachineKeyEncryptor.IsLegacyCiphertext(encrypted).Should().BeFalse();
            CreateEncryptor().RequiresReEncryption(encrypted).Should().BeFalse();
        }

        [Test]
        public void ValuesWithoutThePrefixAreLegacyAndNeedReEncrypting()
        {
            var legacy = LegacySchemeEncryption.Encrypt(legacyKey, "FooBar");

            LinuxMachineKeyEncryptor.IsLegacyCiphertext(legacy).Should().BeTrue();
            CreateEncryptor(legacyKey).RequiresReEncryption(legacy).Should().BeTrue();
        }

        [Test]
        public void EncryptingTheSameValueTwiceProducesDifferentCiphertext()
        {
            var lme = CreateEncryptor();

            var first = lme.Encrypt("FooBar");
            var second = lme.Encrypt("FooBar");

            first.Should().NotBe(second, "every value gets its own IV");
            lme.Decrypt(first).Should().Be("FooBar");
            lme.Decrypt(second).Should().Be("FooBar");
        }

        [Test]
        public void EncryptingNeverTouchesTheLegacyKeys()
        {
            var firstLegacy = MockKey(legacyKey);
            var secondLegacy = MockKey(legacyKey);

            CreateEncryptor(firstLegacy, secondLegacy).Encrypt("FooBar");

            firstLegacy.Received(0).Load();
            secondLegacy.Received(0).Load();
        }

        [Test]
        public void DecryptingACurrentValueNeverTouchesTheLegacyKeys()
        {
            var firstLegacy = MockKey(legacyKey);
            var secondLegacy = MockKey(legacyKey);
            var lme = CreateEncryptor(firstLegacy, secondLegacy);

            lme.Decrypt(lme.Encrypt("FooBar")).Should().Be("FooBar");

            firstLegacy.Received(0).Load();
            secondLegacy.Received(0).Load();
        }

        [Test]
        public void DecryptingACurrentValueWithTheWrongKeyThrows_AndDoesNotFallBackToALegacyKeyThatCouldDecryptIt()
        {
            var otherKey = new InMemoryCryptoKeyNixSource();
            var encryptedWithOtherKey = new LinuxMachineKeyEncryptor(systemLog, otherKey, Array.Empty<ICryptoKeyNixSource>()).Encrypt("FooBar");

            // The legacy list happens to contain the key that would decrypt it. A prefixed value must never take that path.
            var legacyThatWouldWork = MockKey(otherKey);
            var lme = CreateEncryptor(legacyThatWouldWork);

            lme.Invoking(x => x.Decrypt(encryptedWithOtherKey))
                .Should().Throw<CryptographicException>()
                .WithMessage("Unable to decrypt a value protected with the Tentacle machine key.*");
            legacyThatWouldWork.Received(0).Load();
        }

        [Test]
        public void TamperedCurrentValueThrows()
        {
            var lme = CreateEncryptor();
            var encrypted = lme.Encrypt("FooBar, but long enough to span more than one block of AES");

            var payload = Convert.FromBase64String(encrypted.Substring(LinuxMachineKeyEncryptor.ProtectedValuePrefix.Length));
            payload[payload.Length - 1] ^= 0xFF;
            var tampered = LinuxMachineKeyEncryptor.ProtectedValuePrefix + Convert.ToBase64String(payload);

            lme.Invoking(x => x.Decrypt(tampered)).Should().Throw<CryptographicException>();
        }

        [Test]
        public void TruncatedCurrentValueThrows()
        {
            var lme = CreateEncryptor();

            lme.Invoking(x => x.Decrypt(LinuxMachineKeyEncryptor.ProtectedValuePrefix)).Should().Throw<CryptographicException>();
            lme.Invoking(x => x.Decrypt(LinuxMachineKeyEncryptor.ProtectedValuePrefix + "AAAA")).Should().Throw<CryptographicException>();
            lme.Invoking(x => x.Decrypt(LinuxMachineKeyEncryptor.ProtectedValuePrefix + "not base64!")).Should().Throw<CryptographicException>();
        }

        [Test]
        public void GivenCorruptKeyProvided_ThenEncryptThrows()
        {
            var lme = new LinuxMachineKeyEncryptor(systemLog, DodgyKey(), Array.Empty<ICryptoKeyNixSource>());

            lme.Invoking(x => x.Encrypt("FooBar"))
                .Should().Throw<CryptographicException>()
                .WithMessage("Unable to encrypt a value with the Tentacle machine key*");
        }

        [Test]
        public void GivenTheKeyFileCannotBeLoaded_ThenEncryptThrowsWithTheUnderlyingReason()
        {
            var brokenKey = Substitute.For<ICryptoKeyNixSource>();
            brokenKey.Load().Returns(_ => throw new InvalidOperationException("Machine key file at `/etc/octopus/machinekey` is corrupt"));
            var lme = new LinuxMachineKeyEncryptor(systemLog, brokenKey, Array.Empty<ICryptoKeyNixSource>());

            lme.Invoking(x => x.Encrypt("FooBar"))
                .Should().Throw<CryptographicException>()
                .WithMessage("*Machine key file at `/etc/octopus/machinekey` is corrupt*")
                .WithInnerException<InvalidOperationException>();
        }

        [Test]
        public void LegacyValueIsDecryptedWithTheLegacyKeys()
        {
            var legacy = LegacySchemeEncryption.Encrypt(legacyKey, "FooBar");

            CreateEncryptor(legacyKey).Decrypt(legacy).Should().Be("FooBar");
        }

        [Test]
        public void LegacyValue_WhenTheFirstLegacyKeyFails_ThenTheNextIsTried()
        {
            var legacy = LegacySchemeEncryption.Encrypt(legacyKey, "FooBar");

            CreateEncryptor(DodgyKey(), legacyKey).Decrypt(legacy).Should().Be("FooBar");
        }

        [Test]
        public void LegacyValue_TheLegacyKeysAreTriedInOrderAndTheFirstThatWorksWins()
        {
            var firstDodgyKey = DodgyKey();
            var lastKey = MockKey(legacyKey);
            var legacy = LegacySchemeEncryption.Encrypt(legacyKey, "FooBar");

            CreateEncryptor(firstDodgyKey, legacyKey, lastKey).Decrypt(legacy).Should().Be("FooBar");

            firstDodgyKey.Received(1).Load();
            lastKey.Received(0).Load();
        }

        [Test]
        public void LegacyValue_WhenNoLegacyKeyCanDecryptIt_ThenThrows()
        {
            var legacy = LegacySchemeEncryption.Encrypt(legacyKey, "FooBar");

            CreateEncryptor(DodgyKey(), new InMemoryCryptoKeyNixSource())
                .Invoking(x => x.Decrypt(legacy))
                .Should().Throw<AggregateException>();
        }

        [Test]
        public void LegacyValue_IsOnlyDecryptedWithTheLegacyKeys_NotTheCurrentOne()
        {
            // Production puts the generated key in the legacy list as well; that is the composition's job, not the encryptor's.
            var legacyWrittenWithGeneratedKey = LegacySchemeEncryption.Encrypt(generatedKey, "FooBar");

            CreateEncryptor(DodgyKey()).Invoking(x => x.Decrypt(legacyWrittenWithGeneratedKey)).Should().Throw<AggregateException>();
            CreateEncryptor(DodgyKey(), generatedKey).Decrypt(legacyWrittenWithGeneratedKey).Should().Be("FooBar");
        }

        [Test]
        public void LegacyValue_WhenAWrongKeyHappensToProduceValidPadding_ThenTheGarbageIsRejected()
        {
            // Any plaintext Tentacle ever encrypted was UTF-8 text. Decrypting with a wrong key yields random bytes,
            // which are rejected as invalid UTF-8 rather than handed back as a "successfully" decrypted value.
            var notUtf8 = new byte[] { 0xFF, 0xFE, 0xC0, 0xC1, 0xF5, 0x80, 0x80, 0x80, 0xFF, 0xFE, 0xC0, 0xC1, 0xF5, 0x80, 0x80, 0x80 };
            var wrongKeyOutput = LegacySchemeEncryption.Encrypt(legacyKey, notUtf8);

            CreateEncryptor(legacyKey).Invoking(x => x.Decrypt(wrongKeyOutput)).Should().Throw<AggregateException>();
        }

        static ICryptoKeyNixSource DodgyKey()
        {
            var dodgyKey = Substitute.For<ICryptoKeyNixSource>();
            dodgyKey.Load().Returns(callInfo => (new byte[] { 77 }, new byte[] { 43, 11 }));
            return dodgyKey;
        }

        static ICryptoKeyNixSource MockKey(InMemoryCryptoKeyNixSource key)
        {
            var mock = Substitute.For<ICryptoKeyNixSource>();
            mock.Load().Returns(callInfo => (key.Key.ToArray(), key.IV.ToArray()));
            return mock;
        }
    }
}
