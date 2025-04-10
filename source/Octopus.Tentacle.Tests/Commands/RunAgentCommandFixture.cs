using System;
using System.Linq;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Background;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Versioning;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Util;
using Octopus.Time;
using Octopus.Tentacle.Maintenance;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class RunAgentCommandFixture : CommandFixture<RunAgentCommand>
    {
        IHalibutInitializer halibut = null!;
        IWritableTentacleConfiguration tentacleConfiguration = null!;
        ISleep sleep = null!;
        IHomeConfiguration home = null!;
        IApplicationInstanceSelector selector = null!;
        IBackgroundTask[] backgroundTasks = null!;

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

            backgroundTasks = new[]
            {
                Substitute.For<IBackgroundTask>(),
                Substitute.For<IBackgroundTask>()
            };

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
                Substitute.For<ILogFileOnlyLogger>(),
                backgroundTasks.Select(bt => new Lazy<IBackgroundTask>(() => bt)).ToList());

            selector.Current.Returns(new ApplicationInstanceConfiguration("MyTentacle", null, null, null));
        }

        [Test]
        public void WhenCommandIsStartedThenBackgroundServicesAreStarted()
        {
            Start();

            halibut.Received().Start();

            foreach (var backgroundTask in backgroundTasks)
            {
                backgroundTask.Received().Start();
            }
        }

        [Test]
        public void WhenCommandIsStoppedThenBackgroundServicesAreStopped()
        {
            Start();

            Stop();

            halibut.Received().Stop();

            foreach (var backgroundTask in backgroundTasks)
            {
                backgroundTask.Received().Stop();
            }
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