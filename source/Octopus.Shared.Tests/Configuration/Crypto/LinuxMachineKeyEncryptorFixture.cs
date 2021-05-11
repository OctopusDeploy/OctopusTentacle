﻿using System;
using System.Security.Cryptography;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration.Crypto;

namespace Octopus.Shared.Tests.Configuration.Crypto
{
    [TestFixture]
    public class LinuxMachineKeyEncryptorFixture
    {
        readonly InMemoryCryptoKeyNixSource validKey = new InMemoryCryptoKeyNixSource();
        readonly ISystemLog systemLog = Substitute.For<ISystemLog>();
    
        [Test]
        public void EncryptsAndDecrypts()
        {
            var lme = new LinuxMachineKeyEncryptor(systemLog,new[] { validKey });

            var encrypted = lme.Encrypt("FooBar");
            var decrypted = lme.Decrypt(encrypted);

            Assert.AreNotEqual(encrypted, "FooBar");
            Assert.AreEqual(decrypted, "FooBar");
        }

        [Test]
        public void CorruptKeyThrowsException()
        {
            var lme = new LinuxMachineKeyEncryptor(systemLog, new []{DodgyKey()});
            
            Assert.Throws<AggregateException>(() => lme.Encrypt("FooBar"));
        }
        
        [Test]
        public void CorruptKeyWithFallbackSuccessful()
        {
            var lme = new LinuxMachineKeyEncryptor(systemLog, new []{DodgyKey(), validKey});

            var roundTrip = lme.Decrypt(lme.Encrypt("FooBar"));

            roundTrip.Should().Be("FooBar");
        }

        [Test]
        public void KeysAttemptedUntilSucess()
        {
            var firstDodgyKey = DodgyKey();
            var lastDodgyKey = DodgyKey();
            
            var lme = new LinuxMachineKeyEncryptor(systemLog, new []{firstDodgyKey, validKey, lastDodgyKey});
            
            lme.Encrypt("FooBar").Should().NotBeEmpty();
            firstDodgyKey.Received(1).Load();
            lastDodgyKey.Received(0).Load();
        }

        static ICryptoKeyNixSource DodgyKey()
        {
            var dodgyKey = Substitute.For<ICryptoKeyNixSource>();
            dodgyKey.Load().Returns(callInfo => (new byte[] {77}, new byte[]{43, 11}));
            return dodgyKey;
        }

        class InMemoryCryptoKeyNixSource : ICryptoKeyNixSource
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

            public void MakeDodgy()
            {
                key = new byte[] { 11, 23 };
            }
        }
    }
}