using System;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Security;
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
        ITentacleConfiguration tentacleConfiguration;
        ISleep sleep;
        IHomeConfiguration home;
        IApplicationInstanceSelector selector;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            halibut = Substitute.For<IHalibutInitializer>();
            tentacleConfiguration = Substitute.For<ITentacleConfiguration>();
            var certificate = new CertificateGenerator().GenerateNew("cn=Test.Cert.For.Octopus.Tests", new Shared.Diagnostics.NullLog());
            tentacleConfiguration.TentacleCertificate.Returns(certificate);
            home = Substitute.For<IHomeConfiguration>();
            sleep = Substitute.For<ISleep>();
            Command = new RunAgentCommand(
                new Lazy<IHalibutInitializer>(() => halibut),
                new Lazy<ITentacleConfiguration>(() => tentacleConfiguration),
                new Lazy<IHomeConfiguration>(() => home),
                new Lazy<IProxyConfiguration>(() => Substitute.For<IProxyConfiguration>()),
                sleep,
                Substitute.For<ILog>(),
                selector = Substitute.For<IApplicationInstanceSelector>(),
                new Lazy<IProxyInitializer>(() => Substitute.For<IProxyInitializer>()),
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