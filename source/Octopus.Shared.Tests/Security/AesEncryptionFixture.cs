using System;
using NUnit.Framework;
using Octopus.Shared.Security;

namespace Octopus.Shared.Tests.Security
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