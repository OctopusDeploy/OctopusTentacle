using System;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Util;
using Octopus.Tentacle.Versioning;
using Octopus.Time;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class RunAgentCommandFixture : CommandFixture<RunAgentCommand>
    {
        private IHalibutInitializer halibut = null!;
        private IWritableTentacleConfiguration tentacleConfiguration = null!;
        private ISleep sleep = null!;
        private IHomeConfiguration home = null!;
        private IApplicationInstanceSelector selector = null!;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            halibut = Substitute.For<IHalibutInitializer>();
            tentacleConfiguration = Substitute.For<IWritableTentacleConfiguration>();
            var certificate = new CertificateGenerator(new NullLog()).GenerateNew("cn=Test.Cert.For.Octopus.Tests");
            tentacleConfiguration.TentacleCertificate.Returns(certificate);
            home = Substitute.For<IHomeConfiguration>();
            sleep = Substitute.For<ISleep>();

            Command = new RunAgentCommand(
                new Lazy<IHalibutInitializer>(() => halibut),
                new Lazy<IWritableTentacleConfiguration>(() => tentacleConfiguration),
                new Lazy<IHomeConfiguration>(() => home),
                new Lazy<IProxyConfiguration>(() => Substitute.For<IProxyConfiguration>()),
                sleep,
                Substitute.For<ISystemLog>(),
                selector = Substitute.For<IApplicationInstanceSelector>(),
                new Lazy<IProxyInitializer>(() => Substitute.For<IProxyInitializer>()),
                Substitute.For<IWindowsLocalAdminRightsChecker>(),
                new AppVersion(GetType().Assembly),
                Substitute.For<ILogFileOnlyLogger>());

            selector.Current.Returns(new ApplicationInstanceConfiguration("MyTentacle", null, null, null));
        }

        [Test]
        public void RunsBackgroundServices()
        {
            Start();
            halibut.Received().Start();
        }

        [Test]
        public void WaitsBeforeStarting()
        {
            Start("/wait=2000");

            sleep.Received().For(2000);

            halibut.Received().Start();
        }
    }
}