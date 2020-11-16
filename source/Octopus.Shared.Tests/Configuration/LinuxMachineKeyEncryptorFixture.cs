using System;
using System.IO;
using NUnit.Framework;
using Octopus.Shared.Configuration;

namespace Octopus.Shared.Tests.Configuration
{
    [TestFixture]
    public class LinuxMachineKeyEncryptorFixture
    {
        static readonly string OriginalKeyFileName = LinuxMachineKeyEncryptor.LinuxMachineKey.FileName;
        string tempKeyFileName;

        [SetUp]
        public void Setup()
        {
            tempKeyFileName = Path.GetTempFileName();
            File.Delete(tempKeyFileName);
            LinuxMachineKeyEncryptor.LinuxMachineKey.FileName = tempKeyFileName;
        }

        [TearDown]
        public void TearDown()
        {
            LinuxMachineKeyEncryptor.LinuxMachineKey.FileName = OriginalKeyFileName;
            if (File.Exists(tempKeyFileName))
                File.Delete(tempKeyFileName);
        }

        [Test]
        public void EncryptsAndDecrypts()
        {
            var lme = new LinuxMachineKeyEncryptor();
            var encrypted = lme.Encrypt("FooBar");
            var decrypted = lme.Decrypt(encrypted);
            Assert.AreNotEqual(encrypted, "FooBar");
            Assert.AreEqual(decrypted, "FooBar");
        }

        [Test]
        public void CorruptKeyThrowsException()
        {
            File.WriteAllText(tempKeyFileName, "IAMAKEY");
            var lme = new LinuxMachineKeyEncryptor();
            Assert.Throws<InvalidOperationException>(() => lme.Encrypt("FooBar"));
        }
    }
}