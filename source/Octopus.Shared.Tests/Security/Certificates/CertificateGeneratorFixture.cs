using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Security;
using Octopus.Shared.Tests.Support;

namespace Octopus.Shared.Tests.Security.Certificates
{
    [TestFixture]
    public class CertificateGeneratorFixture
    {
        readonly CertificateGenerator generator = new CertificateGenerator(new InMemoryLog());

        [Test]
        public void CanGenerateExportableCertificates()
        {
            var log = new InMemoryLog();
            var cert = generator.GenerateNew("CN=test");
            cert.Export(X509ContentType.Pkcs12);
            Assert.That(cert.PrivateKey.KeySize, Is.EqualTo(2048));
            Assert.That(cert.PublicKey.Key.KeySize, Is.EqualTo(2048));
            if (cert.SignatureAlgorithm.FriendlyName == "sha1RSA")
                log.GetLog().Should().Contain("WARN: Falling back to SHA1 certificate");
            else
                log.GetLog().Should().NotContain("Falling back to SHA1 certificate");
            Assert.That(cert.SubjectName.Name, Is.EqualTo("CN=test"));
            Assert.That(cert.Issuer, Is.EqualTo("CN=test"));
        }

        [Test]
        [WindowsTest]
        public void CanGenerateNonExportableCertificates()
        {
            var cert = generator.GenerateNewNonExportable("CN=test");
            Action act = () => cert.Export(X509ContentType.Pkcs12);
            // Pkcs12 exports include the private key - since the cert is non-exportable, this isn't allowed
            act.Should().Throw<CryptographicException>().WithMessage("Key not valid for use in specified state*");
        }
    }
}