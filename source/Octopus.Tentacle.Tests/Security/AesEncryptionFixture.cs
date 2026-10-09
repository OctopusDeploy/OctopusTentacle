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

        [Test]
        public void CanDecryptDataEncryptedWithLegacyKeyDerivation()
        {
            // Encrypted with a key from new Rfc2898DeriveBytes("purple-monkey-dishwasher", "Octopuss", 1000).GetBytes(16)
            // (SHA1) and IV 00..0F, to guard against changes to the key derivation breaking existing encrypted data.
            var encrypted = Convert.FromBase64String("SVZfXwABAgMEBQYHCAkKCwwNDg98YoLlooiJfLbZq0pAmFcugUuM8KPSrMtK6FlU9RTwEPSBaeN08FxgIlmsqL9hTzQ=");
            var decrypted = new AesEncryption("purple-monkey-dishwasher").Decrypt(encrypted);
            Assert.AreEqual("Hello from the old key derivation", decrypted);
        }

        [Test]
        public void PasswordWithInvalidUtf16DoesNotThrow()
        {
            var encryptor = new AesEncryption("bad\uD800surrogate");
            var decrypted = encryptor.Decrypt(encryptor.Encrypt("FooBar"));
            Assert.AreEqual("FooBar", decrypted);
        }
    }
}