using System;
using NUnit.Framework;
using Octopus.Shared.Configuration;

namespace Octopus.Shared.Tests.Configuration.Crypto
{
    [TestFixture]
    [WindowsTest]
    public class WindowsMachineKeyEncryptorFixture
    {
        [Test]
        public void EncryptsAndDecrypts()
        {
            var wme = new WindowsMachineKeyEncryptor();
            var encrypted = wme.Encrypt("FooBar");
            var decrypted = wme.Decrypt(encrypted);
            Assert.AreNotEqual(encrypted, "FooBar");
            Assert.AreEqual(decrypted, "FooBar");
        }
    }
}