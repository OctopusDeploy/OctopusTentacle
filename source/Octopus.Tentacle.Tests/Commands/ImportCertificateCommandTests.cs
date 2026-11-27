using System;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Commands.Support;

namespace Octopus.Tentacle.Tests.Commands
{
    public class ImportCertificateCommandTests
    {
        [Test]
        public void ShouldThrowExceptionWhenNoCertificateIsSpecified()
        {
            var command = CommandBuilder.ImportCertificate();

            var exception = Assert.Throws<ControlledFailureException>(() => command.StartWith());
            exception.Message.Should().Be("Please specify the certificate to import.");
        }

        [Test]
        public void ShouldThrowExceptionWhenMultipleCertificatesAreSpecified()
        {
            var command = CommandBuilder.ImportCertificate();

            var exception = Assert.Throws<ControlledFailureException>(() => command.StartWith("-r", "-b qwerty"));
            exception.Message.Should().Be("Please specify only one of either from-registry or from-file or from-base64");
        }

        [Test]
        public void ShouldImportCertificateFromBase64()
        {
            var configuration = Substitute.For<IWritableTentacleConfiguration>();
            var command = CommandBuilder.ImportCertificate(configuration);

            var mockCert = new CertificateGenerator(new NullLog()).GenerateNew("CN=Hello");
            var base64Cert = Convert.ToBase64String(mockCert.RawData);
            command.StartWith($"-b {base64Cert}");

            configuration.Received().ImportCertificate(Arg.Is<X509Certificate2>(cert => cert.Thumbprint == mockCert.Thumbprint));
        }
    }
}
