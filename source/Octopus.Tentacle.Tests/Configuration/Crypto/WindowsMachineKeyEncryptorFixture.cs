using System;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Tests.Support;

namespace Octopus.Tentacle.Tests.Configuration.Crypto
{
    [TestFixture]
    [Support.TestAttributes.WindowsTest]
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