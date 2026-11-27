using System;
using System.Collections.Generic;
using System.Linq;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Background;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Commands.Support;
using Octopus.Tentacle.Util;
using Octopus.Tentacle.Versioning;
using Octopus.Time;

namespace Octopus.Tentacle.Tests.Commands
{
    public class RunAgentCommandTests
    {
        [Test]
        public void WhenCommandIsStartedThenBackgroundServicesAreStarted()
        {
            var halibut = Substitute.For<IHalibutInitializer>();
            var backgroundTasks = new[] { Substitute.For<IBackgroundTask>(), Substitute.For<IBackgroundTask>() };
            var command = CreateCommand(halibut, backgroundTasks: backgroundTasks);

            command.StartWith();

            halibut.Received().Start();

            foreach (var backgroundTask in backgroundTasks)
            {
                backgroundTask.Received().Start();
            }
        }

        [Test]
        public void WhenCommandIsStoppedThenBackgroundServicesAreStopped()
        {
            var halibut = Substitute.For<IHalibutInitializer>();
            var backgroundTasks = new[] { Substitute.For<IBackgroundTask>(), Substitute.For<IBackgroundTask>() };
            ICommand command = CreateCommand(halibut, backgroundTasks: backgroundTasks);

            command.StartWith();

            command.Stop();

            halibut.Received().Stop();

            foreach (var backgroundTask in backgroundTasks)
            {
                backgroundTask.Received().Stop();
            }
        }

        [Test]
        public void WaitsBeforeStarting()
        {
            var halibut = Substitute.For<IHalibutInitializer>();
            var sleep = Substitute.For<ISleep>();
            var command = CreateCommand(halibut, sleep);

            command.StartWith("/wait=2000");

            sleep.Received().For(2000);

            halibut.Received().Start();
        }

        static RunAgentCommand CreateCommand(IHalibutInitializer halibut, ISleep? sleep = null, IEnumerable<IBackgroundTask>? backgroundTasks = null)
        {
            var tentacleConfiguration = Substitute.For<IWritableTentacleConfiguration>();
            tentacleConfiguration.TentacleCertificate.Returns(new CertificateGenerator(new NullLog()).GenerateNew("cn=Test.Cert.For.Octopus.Tests"));
            var selector = Substitute.For<IApplicationInstanceSelector>();
            selector.Current.Returns(new ApplicationInstanceConfiguration("MyTentacle", null, null, null, null));

            return new RunAgentCommand(new Lazy<IHalibutInitializer>(() => halibut),
                new Lazy<IWritableTentacleConfiguration>(() => tentacleConfiguration),
                new Lazy<IHomeConfiguration>(() => Substitute.For<IHomeConfiguration>()),
                new Lazy<IProxyConfiguration>(() => Substitute.For<IProxyConfiguration>()),
                sleep ?? Substitute.For<ISleep>(),
                Substitute.For<ISystemLog>(),
                selector,
                new Lazy<IProxyInitializer>(() => Substitute.For<IProxyInitializer>()),
                Substitute.For<IWindowsLocalAdminRightsChecker>(),
                new AppVersion(typeof(RunAgentCommandTests).Assembly),
                Substitute.For<ILogFileOnlyLogger>(),
                (backgroundTasks ?? Enumerable.Empty<IBackgroundTask>()).Select(bt => new Lazy<IBackgroundTask>(() => bt)).ToList(),
                Substitute.For<IConfigurationFilePermissions>(),
                new Lazy<IConfigurationEncryptionUpgrade>(() => Substitute.For<IConfigurationEncryptionUpgrade>()));
        }
    }
}
