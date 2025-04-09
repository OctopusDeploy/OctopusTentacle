using System;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class NewCertificateCommandFixture : CommandFixture<NewCertificateCommand>
    {
        StubTentacleConfiguration configuration = null!;
        ISystemLog log = null!;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            log = Substitute.For<ISystemLog>();
            configuration = new StubTentacleConfiguration();
            var selector = Substitute.For<IApplicationInstanceSelector>();
            selector.Current.Returns(info => new ApplicationInstanceConfiguration(null, null!, null!, null!));
            Command = new NewCertificateCommand(new Lazy<IWritableTentacleConfiguration>(() => configuration), log, selector, new Lazy<ICertificateGenerator>(() => Substitute.For<ICertificateGenerator>()), Substitute.For<ILogFileOnlyLogger>());
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