using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Shared.Security.Certificates;

namespace Octopus.Shared.Tests.Security.Certificates
{
    [TestFixture]
    public class CertificateEncoderFixture
    {
        [Test]
        public void GivenPfxWithPrivateKeyAndPasswordThenReturnsCertificate()
        {
            // Given
            var pfxFilePath = GetPfxFilePath("TestCertificateWithPassword.pfx");

            // When
            var cert = CertificateEncoder.FromPfxFile(pfxFilePath, "Password01!", Substitute.For<ILog>());

            // Then
            try
            {
                Assert.That(cert, Is.Not.Null);
            }
            finally
            {
                File.Delete(pfxFilePath);
            }
        }
        
        
        [Test]
        public void WhenGivenABase64StringOfACertificate_ACertificateCanBeCreated()
        {
            // Given
            var base64Cert = CertificateEncoder.ToBase64String(Certificate());

            var certFromBase64 = CertificateEncoder.FromBase64String(base64Cert, Substitute.For<ILog>());
            // Then
            Assert.That(certFromBase64, Is.Not.Null);
        }

        [Test]
        [WindowsTest]
        public void GivenPfxWithPasswordButDontUseItThenThrows()
        {
            // Given
            var pfxFilePath = GetPfxFilePath("TestCertificateWithPassword.pfx");
            Action action = () => CertificateEncoder.FromPfxFile(pfxFilePath, "", Substitute.For<ILog>());

            try
            {
                action.Should().Throw<CryptographicException>().WithMessage("The specified network password is not correct*");
            }
            finally
            {
                File.Delete(pfxFilePath);
            }
        }

        [Test]
        public void GivenPfxWithOneCertWithPrivateKeyAndEmptyPasswordThenReturnsCertificate()
        {
            // Given
            var pfxFilePath = GetPfxFilePath("TestCertificateNoPassword.pfx");

            // When
            var cert = CertificateEncoder.FromPfxFile(pfxFilePath, "", Substitute.For<ILog>());

            // Then
            try
            {
                Assert.That(cert, Is.Not.Null);
            }
            finally
            {
                File.Delete(pfxFilePath);
            }
        }

        [Test]
        public void GivenPfxWithOneCertWithoutPrivateKeyThenThrows()
        {
            // Given
            var pfxFilePath = GetPfxFilePath("TestCertificateNoPrivateKey.pfx");

            Action action = () => CertificateEncoder.FromPfxFile(pfxFilePath, "Password01!", Substitute.For<ILog>());

            try
            {
                action.Should()
                    .Throw<CryptographicException>()
                    .WithMessage("Unable to load X509 Certificate file. The X509 certificate file you provided does not include the private key. Please make sure the private key is included in your X509 certificate file and try again.");
            }
            finally
            {
                File.Delete(pfxFilePath);
            }
        }

        static string GetPfxFilePath(string pfxFileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Octopus.Shared.Tests.Resources." + pfxFileName;
            var stream = assembly.GetManifestResourceStream(resourceName);
            var bytes = GetBytesFromStream(stream);
            var pfxFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pfx");
            File.WriteAllBytes(pfxFilePath, bytes);
            return pfxFilePath;
        }

        public static byte[] GetBytesFromStream(Stream input)
        {
            using (var ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }
        
        static X509Certificate2 Certificate()
        {
            var pfxFilePath = GetPfxFilePath("TestCertificateWithPassword.pfx");
            try
            {
                return CertificateEncoder.FromPfxFile(pfxFilePath, "Password01!", Substitute.For<ILog>());
            }
            finally
            {
                File.Delete(pfxFilePath);
            }
        }
    }
}