using System;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Startup;
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
            var command = CreateCommand(configuration, log);

            command.StartWith();

            Assert.That(configuration.TentacleCertificate, Is.Not.Null);

            log.Received().Info(configuration.TentacleCertificate.Thumbprint);
        }

        static NewCertificateCommand CreateCommand(StubTentacleConfiguration configuration, ISystemLog log)
        {
            return new NewCertificateCommand(new Lazy<IWritableTentacleConfiguration>(() => configuration),
                log,
                UnnamedInstanceSelector(),
                new Lazy<ICertificateGenerator>(() => Substitute.For<ICertificateGenerator>()),
                Substitute.For<ILogFileOnlyLogger>());
        }

        static IApplicationInstanceSelector UnnamedInstanceSelector()
        {
            var selector = Substitute.For<IApplicationInstanceSelector>();
            selector.Current.Returns(_ => new ApplicationInstanceConfiguration(null, null!, null!, null!, null));
            return selector;
        }
    }
}
