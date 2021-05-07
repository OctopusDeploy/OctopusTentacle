using System;
using System.Security.Cryptography;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Shared.Configuration;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    public class LinuxMachineKeyEncryptorFixture
    {
        readonly InMemoryCryptoKeyNixSource validKey = new InMemoryCryptoKeyNixSource();

        [Test]
        public void EncryptsAndDecrypts()
        {
            var lme = new LinuxMachineKeyEncryptor(new[] { validKey });

            var encrypted = lme.Encrypt("FooBar");
            var decrypted = lme.Decrypt(encrypted);

            Assert.AreNotEqual(encrypted, "FooBar");
            Assert.AreEqual(decrypted, "FooBar");
        }

        [Test]
        public void CorruptKeyThrowsException()
        {
            var lme = new LinuxMachineKeyEncryptor(new []{DodgyKey()});
            
            Assert.Throws<AggregateException>(() => lme.Encrypt("FooBar"));
        }
        
        [Test]
        public void CorruptKeyWithFallbackSuccessful()
        {
            var lme = new LinuxMachineKeyEncryptor(new []{DodgyKey(), validKey});

            var roundTrip = lme.Decrypt(lme.Encrypt("FooBar"));

            roundTrip.Should().Be("FooBar");
        }

        [Test]
        public void KeysAttemptedUntilSucess()
        {
            var firstDodgyKey = DodgyKey();
            var lastDodgyKey = DodgyKey();
            
            var lme = new LinuxMachineKeyEncryptor(new []{firstDodgyKey, validKey, lastDodgyKey});
            
            lme.Encrypt("FooBar").Should().NotBeEmpty();
            firstDodgyKey.Received(1).Load();
            lastDodgyKey.Received(0).Load();
        }

        static LinuxMachineKeyEncryptor.ICryptoKeyNixSource DodgyKey()
        {
            return Substitute.For<LinuxMachineKeyEncryptor.ICryptoKeyNixSource>();
        }

        class InMemoryCryptoKeyNixSource : LinuxMachineKeyEncryptor.ICryptoKeyNixSource
        {
            byte[] key;
            readonly byte[] iv;
            public InMemoryCryptoKeyNixSource()
            {
                var d = new RijndaelManaged();
                d.GenerateIV();
                d.GenerateKey();
                key = d.Key;
                iv = d.IV;
            }
            
            public (byte[] Key, byte[] IV) Load()
            {
                return (key, iv);
            }
        }
    }
}