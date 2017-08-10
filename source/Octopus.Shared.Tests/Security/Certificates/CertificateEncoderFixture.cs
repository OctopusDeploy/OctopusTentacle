using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
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
            var cert = CertificateEncoder.FromPfxFile(pfxFilePath, "Password01!");

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
        [ExpectedException(typeof(CryptographicException), ExpectedMessage = "The specified network password is not correct.", MatchType = MessageMatch.StartsWith)]
        public void GivenPfxWithPasswordButDontUseItThenThrows()
        {
            // Given
            var pfxFilePath = GetPfxFilePath("TestCertificateWithPassword.pfx");

            // When / Then
            try
            {
                CertificateEncoder.FromPfxFile(pfxFilePath, "");
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
            var cert = CertificateEncoder.FromPfxFile(pfxFilePath, "");

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
        [ExpectedException(typeof(CryptographicException), ExpectedMessage = "Unable to load X509 Certificate file. The X509 certificate file you provided does not include the private key. Please make sure the private key is included in your X509 certificate file and try again.")]
        public void GivenPfxWithOneCertWithoutPrivateKeyThenThrows()
        {
            // Given
            var pfxFilePath = GetPfxFilePath("TestCertificateNoPrivateKey.pfx");

            // When / Then
            try
            {
                CertificateEncoder.FromPfxFile(pfxFilePath, "Password01!");
            }
            finally
            {
                File.Delete(pfxFilePath);
            }
            Assert.Fail();
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
            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}