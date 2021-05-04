using System;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Util;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Versioning;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Time;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class RunAgentCommandFixture : CommandFixture<RunAgentCommand>
    {
        IHalibutInitializer halibut;
        IWritableTentacleConfiguration tentacleConfiguration;
        ISleep sleep;
        IHomeConfiguration home;
        IApplicationInstanceSelector selector;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            halibut = Substitute.For<IHalibutInitializer>();
            tentacleConfiguration = Substitute.For<IWritableTentacleConfiguration>();
            var certificate = new CertificateGenerator(new Shared.Diagnostics.NullLog()).GenerateNew("cn=Test.Cert.For.Octopus.Tests");
            tentacleConfiguration.TentacleCertificate.Returns(certificate);
            home = Substitute.For<IHomeConfiguration>();
            sleep = Substitute.For<ISleep>();
            Command = new RunAgentCommand(
                new StartUpPersistedInstanceRequest(ApplicationName.Tentacle, "MyTentacle"),
                new Lazy<IHalibutInitializer>(() => halibut),
                new Lazy<IWritableTentacleConfiguration>(() => tentacleConfiguration),
                new Lazy<IHomeConfiguration>(() => home),
                new Lazy<IProxyConfiguration>(() => Substitute.For<IProxyConfiguration>()),
                sleep,
                Substitute.For<ISystemLog>(),
                selector = Substitute.For<IApplicationInstanceSelector>(),
                new Lazy<IProxyInitializer>(() => Substitute.For<IProxyInitializer>()),
                Substitute.For<IWindowsLocalAdminRightsChecker>(),
                new AppVersion(GetType().Assembly));

            selector.GetCurrentName().Returns("MyTentacle");
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
