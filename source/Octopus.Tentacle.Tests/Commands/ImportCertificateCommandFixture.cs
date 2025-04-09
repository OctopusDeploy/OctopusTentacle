using System;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class ImportCertificateCommandFixture : CommandFixture<ImportCertificateCommand>
    {
        IWritableTentacleConfiguration configuration;

        [SetUp]
        public void BeforeEachTest()
        {
            configuration = Substitute.For<IWritableTentacleConfiguration>();
            var selector = Substitute.For<IApplicationInstanceSelector>();
            selector.Current.Returns(_ => new ApplicationInstanceConfiguration(null, null!, null!, null!));
            Command = new ImportCertificateCommand(
                new Lazy<IWritableTentacleConfiguration>(() => configuration),
                Substitute.For<ISystemLog>(),
                selector,
                Substitute.For<ILogFileOnlyLogger>()
            );
        }

        [Test]
        public void ShouldThrowExceptionWhenNoCertificateIsSpecified()
        {
            var exception = Assert.Throws<ControlledFailureException>(() => Start());
            exception.Message.Should().Be("Please specify the certificate to import.");
        }
        
        [Test]
        public void ShouldThrowExceptionWhenMultipleCertificatesAreSpecified()
        {
            var exception = Assert.Throws<ControlledFailureException>(() => Start("-r", "-b qwerty"));
            exception.Message.Should().Be("Please specify only one of either from-registry or from-file or from-base64");
        }

        [Test]
        public void ShouldImportCertificateFromBase64()
        {
            var mockCert = new CertificateGenerator(new NullLog()).GenerateNew("CN=Hello");
            var base64Cert = Convert.ToBase64String(mockCert.RawData);
            Start($"-b {base64Cert}");
            
            configuration.Received().ImportCertificate(Arg.Is<X509Certificate2>(cert => cert.Thumbprint == mockCert.Thumbprint));
        }
    }
}