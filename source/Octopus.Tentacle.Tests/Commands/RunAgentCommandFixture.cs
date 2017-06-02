using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octopus.Shared.Configuration;
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
            var certificate = new CertificateGenerator().GenerateNew("cn=Test.Cert.For.Octopus.Tests");
            tentacleConfiguration.TentacleCertificate.Returns(certificate);
            home = Substitute.For<IHomeConfiguration>();
            sleep = Substitute.For<ISleep>();
            Command = new RunAgentCommand(
                halibut,
                tentacleConfiguration,
                home,
                Substitute.For<IProxyConfiguration>(), 
                sleep, 
                Substitute.For<ILog>(),
                selector = Substitute.For<IApplicationInstanceSelector>(),
                Substitute.For<IProxyInitializer>(),
                new AppVersion(GetType().Assembly));

            selector.Current.Returns(new LoadedApplicationInstance(ApplicationName.Tentacle, "MyTentacle", "", new DictionaryKeyValueStore()));
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