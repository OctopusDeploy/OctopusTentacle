using System;
using System.Collections.Generic;
using System.Linq;
using NSubstitute;
using Octopus.Client.Operations;
using Octopus.Tentacle.Background;
using Octopus.Tentacle.Certificates;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Commands.OptionSets;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Diagnostics;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Maintenance;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Util;
using Octopus.Tentacle.Versioning;
using Octopus.Time;

namespace Octopus.Tentacle.Tests.Commands.Support
{
    static class CommandBuilder
    {
        public static ConfigureCommand Configure(StubTentacleConfiguration configuration, IOctopusFileSystem? fileSystem = null)
        {
            return new ConfigureCommand(new Lazy<IWritableTentacleConfiguration>(() => configuration),
                new Lazy<IWritableHomeConfiguration>(() => new StubHomeConfiguration()),
                fileSystem ?? Substitute.For<IOctopusFileSystem>(),
                Substitute.For<ISystemLog>(),
                UnnamedInstanceSelector(),
                Substitute.For<ILogFileOnlyLogger>());
        }

        public static DeregisterMachineCommand DeregisterMachine(ITentacleConfiguration configuration, ISystemLog? log = null)
        {
            return new DeregisterMachineCommand(new Lazy<ITentacleConfiguration>(() => configuration),
                log ?? Substitute.For<ISystemLog>(),
                NamedInstanceSelector(),
                Substitute.For<IProxyConfigParser>(),
                Substitute.For<IOctopusClientInitializer>(),
                new SpaceRepositoryFactory(),
                Substitute.For<ILogFileOnlyLogger>());
        }

        public static DeregisterWorkerCommand DeregisterWorker(ITentacleConfiguration configuration, ISystemLog? log = null)
        {
            return new DeregisterWorkerCommand(new Lazy<ITentacleConfiguration>(() => configuration),
                log ?? Substitute.For<ISystemLog>(),
                NamedInstanceSelector(),
                Substitute.For<IProxyConfigParser>(),
                Substitute.For<IOctopusClientInitializer>(),
                new SpaceRepositoryFactory(),
                Substitute.For<ILogFileOnlyLogger>());
        }

        public static ImportCertificateCommand ImportCertificate(IWritableTentacleConfiguration? configuration = null)
        {
            configuration ??= Substitute.For<IWritableTentacleConfiguration>();
            return new ImportCertificateCommand(new Lazy<IWritableTentacleConfiguration>(() => configuration),
                Substitute.For<ISystemLog>(),
                UnnamedInstanceSelector(),
                Substitute.For<ILogFileOnlyLogger>());
        }

        public static ListInstancesCommand ListInstances()
        {
            return new ListInstancesCommand(Substitute.For<IApplicationInstanceStore>(), Substitute.For<ILogFileOnlyLogger>());
        }

        public static LivezCommand Livez(string homeDirectory, IClock clock)
        {
            var homeConfiguration = Substitute.For<IHomeConfiguration>();
            homeConfiguration.HomeDirectory.Returns(homeDirectory);

            return new LivezCommand(new Lazy<IHomeConfiguration>(() => homeConfiguration),
                clock,
                UnnamedInstanceSelector(),
                Substitute.For<ISystemLog>(),
                Substitute.For<ILogFileOnlyLogger>());
        }

        public static NewCertificateCommand NewCertificate(StubTentacleConfiguration configuration, ISystemLog log)
        {
            return new NewCertificateCommand(new Lazy<IWritableTentacleConfiguration>(() => configuration),
                log,
                UnnamedInstanceSelector(),
                new Lazy<ICertificateGenerator>(() => Substitute.For<ICertificateGenerator>()),
                Substitute.For<ILogFileOnlyLogger>());
        }

        public static ProxyConfigurationCommand ProxyConfiguration(Lazy<IWritableProxyConfiguration> configuration)
        {
            return new ProxyConfigurationCommand(configuration, UnnamedInstanceSelector(), Substitute.For<ISystemLog>(), Substitute.For<ILogFileOnlyLogger>());
        }

        public static RunAgentCommand RunAgent(IHalibutInitializer halibut, ISleep? sleep = null, IEnumerable<IBackgroundTask>? backgroundTasks = null)
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
                new AppVersion(typeof(CommandBuilder).Assembly),
                Substitute.For<ILogFileOnlyLogger>(),
                (backgroundTasks ?? Enumerable.Empty<IBackgroundTask>()).Select(bt => new Lazy<IBackgroundTask>(() => bt)).ToList(),
                Substitute.For<IConfigurationFilePermissions>(),
                new Lazy<IConfigurationEncryptionUpgrade>(() => Substitute.For<IConfigurationEncryptionUpgrade>()));
        }

        public static SetPollingConnectionCountCommand SetPollingConnectionCount(StubTentacleConfiguration configuration)
        {
            return new SetPollingConnectionCountCommand(new Lazy<IWritableTentacleConfiguration>(() => configuration),
                UnnamedInstanceSelector(),
                Substitute.For<ISystemLog>(),
                Substitute.For<ILogFileOnlyLogger>());
        }

        public static RegisterMachineCommand RegisterMachine(RegistrationSubstitutes<IRegisterMachineOperation> substitutes, IPrompt trustPrompt)
        {
            return new RegisterMachineCommand(new Lazy<IRegisterMachineOperation>(() => substitutes.Operation),
                new Lazy<IWritableTentacleConfiguration>(() => substitutes.Configuration),
                substitutes.Log,
                UnnamedInstanceSelector(),
                new Lazy<IOctopusServerChecker>(() => substitutes.ServerChecker),
                new ProxyConfigParser(),
                substitutes.ClientInitializer,
                new SpaceRepositoryFactory(),
                Substitute.For<ILogFileOnlyLogger>(),
                new ServerCertificateTrustConfirmation(substitutes.Log, trustPrompt));
        }

        public static RegisterWorkerCommand RegisterWorker(RegistrationSubstitutes<IRegisterWorkerOperation> substitutes)
        {
            return new RegisterWorkerCommand(new Lazy<IRegisterWorkerOperation>(() => substitutes.Operation),
                new Lazy<IWritableTentacleConfiguration>(() => substitutes.Configuration),
                substitutes.Log,
                UnnamedInstanceSelector(),
                new Lazy<IOctopusServerChecker>(() => substitutes.ServerChecker),
                new ProxyConfigParser(),
                substitutes.ClientInitializer,
                new SpaceRepositoryFactory(),
                Substitute.For<ILogFileOnlyLogger>(),
                Substitute.For<IServerCertificateTrustConfirmation>());
        }

        public static RegisterKubernetesDeploymentTargetCommand RegisterKubernetesDeploymentTarget(RegistrationSubstitutes<IRegisterKubernetesDeploymentTargetOperation> substitutes)
        {
            return new RegisterKubernetesDeploymentTargetCommand(new Lazy<IRegisterKubernetesDeploymentTargetOperation>(() => substitutes.Operation),
                new Lazy<IWritableTentacleConfiguration>(() => substitutes.Configuration),
                substitutes.Log,
                UnnamedInstanceSelector(),
                new Lazy<IOctopusServerChecker>(() => substitutes.ServerChecker),
                new ProxyConfigParser(),
                substitutes.ClientInitializer,
                new SpaceRepositoryFactory(),
                Substitute.For<ILogFileOnlyLogger>(),
                Substitute.For<IServerCertificateTrustConfirmation>());
        }

        public static RegisterKubernetesWorkerCommand RegisterKubernetesWorker(RegistrationSubstitutes<IRegisterKubernetesWorkerOperation> substitutes)
        {
            return new RegisterKubernetesWorkerCommand(new Lazy<IRegisterKubernetesWorkerOperation>(() => substitutes.Operation),
                new Lazy<IWritableTentacleConfiguration>(() => substitutes.Configuration),
                substitutes.Log,
                UnnamedInstanceSelector(),
                new Lazy<IOctopusServerChecker>(() => substitutes.ServerChecker),
                new ProxyConfigParser(),
                substitutes.ClientInitializer,
                new SpaceRepositoryFactory(),
                Substitute.For<ILogFileOnlyLogger>(),
                Substitute.For<IServerCertificateTrustConfirmation>());
        }

        static IApplicationInstanceSelector UnnamedInstanceSelector()
        {
            var selector = Substitute.For<IApplicationInstanceSelector>();
            selector.Current.Returns(_ => new ApplicationInstanceConfiguration(null, null!, null!, null!, null));
            return selector;
        }

        static IApplicationInstanceSelector NamedInstanceSelector()
        {
            var selector = Substitute.For<IApplicationInstanceSelector>();
            selector.Current.Returns(new ApplicationInstanceConfiguration("my-instance", "myconfig.config", Substitute.For<IKeyValueStore>(), Substitute.For<IWritableKeyValueStore>(), null));
            return selector;
        }
    }
}
