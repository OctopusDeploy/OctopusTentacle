using System;
using NUnit.Framework;
using Octopus.Tentacle.Security;

namespace Octopus.Tentacle.Tests.Security
{
    [TestFixture]
    public class AesEncryptionFixture
    {
        [Test]
        public void EncryptionIsSymmetrical()
        {
            var password = "purple-monkey-dishwasher";
            var encryptor = new AesEncryption(password);
            var encrypted = encryptor.Encrypt("FooBar");
            var decrypted = encryptor.Decrypt(encrypted);
            Assert.AreEqual("FooBar", decrypted);
        }
    }
}