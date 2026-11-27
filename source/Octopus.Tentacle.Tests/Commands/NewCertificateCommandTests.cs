using System;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Tests.Commands.Support;

namespace Octopus.Tentacle.Tests.Commands
{
    public class NewCertificateCommandTests
    {
        [Test]
        public void ShouldCreateNewCertificateAndWriteThumbprintToStdout()
        {
            var log = Substitute.For<ISystemLog>();
            var configuration = new StubTentacleConfiguration();
            var command = CommandBuilder.NewCertificate(configuration, log);

            command.StartWith();

            Assert.That(configuration.TentacleCertificate, Is.Not.Null);

            log.Received().Info(configuration.TentacleCertificate.Thumbprint);
        }
    }
}
