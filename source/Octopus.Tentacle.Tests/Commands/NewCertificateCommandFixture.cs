using System;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Security;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Configuration;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class NewCertificateCommandFixture : CommandFixture<NewCertificateCommand>
    {
        StubTentacleConfiguration configuration;
        ILog log;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            log = Substitute.For<ILog>();
            configuration = new StubTentacleConfiguration();
            Command = new NewCertificateCommand(new Lazy<IWritableTentacleConfiguration>(() => configuration), log, Substitute.For<IApplicationInstanceSelector>(), new Lazy<ICertificateGenerator>(() => Substitute.For<ICertificateGenerator>()));
        }

        [Test]
        public void ShouldCreateNewCertificateAndWriteThumbprintToStdout()
        {
            Start();

            Assert.That(configuration.TentacleCertificate, Is.Not.Null);

            log.Received().Info(configuration.TentacleCertificate.Thumbprint);
        }
    }
}