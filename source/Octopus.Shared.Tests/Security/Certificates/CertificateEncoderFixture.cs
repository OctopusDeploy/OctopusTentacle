using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using FluentAssertions;
using NSubstitute.ExceptionExtensions;
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
        public void GivenPfxWithPasswordButDontUseItThenThrows()
        {
            // Given
            var pfxFilePath = GetPfxFilePath("TestCertificateWithPassword.pfx");
            Action action = () => CertificateEncoder.FromPfxFile(pfxFilePath, "");

            try
            { 
                action.ShouldThrow<CryptographicException>().WithMessage("The specified network password is not correct.");
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
        public void GivenPfxWithOneCertWithoutPrivateKeyThenThrows()
        {
            // Given
            var pfxFilePath = GetPfxFilePath("TestCertificateNoPrivateKey.pfx");

            Action action = () => CertificateEncoder.FromPfxFile(pfxFilePath, "Password01!");

            try
            {
                action.ShouldThrow<CryptographicException>()
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
            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}