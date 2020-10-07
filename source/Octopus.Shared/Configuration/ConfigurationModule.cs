using Autofac;
using Octopus.Configuration;
using Octopus.Shared.Configuration.EnvironmentVariableMappings;
using Octopus.Shared.Configuration.Instances;
using Octopus.Shared.Services;
using Octopus.Shared.Startup;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class ConfigurationModule : Module
    {
        readonly StartUpInstanceRequest startUpInstanceRequest;

        public ConfigurationModule(StartUpInstanceRequest startUpInstanceRequest)
        {
            this.startUpInstanceRequest = startUpInstanceRequest;
        }

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            if (PlatformDetection.IsRunningOnWindows)
            {
                builder.RegisterType<RegistryApplicationInstanceStore>()
                    .WithParameter("startUpInstanceRequest", startUpInstanceRequest)
                    .As<IRegistryApplicationInstanceStore>();
                builder.RegisterType<WindowsServiceConfigurator>().As<IServiceConfigurator>();
            }
            else
            {
                builder.RegisterType<NullRegistryApplicationInstanceStore>().As<IRegistryApplicationInstanceStore>();
                builder.RegisterType<LinuxServiceConfigurator>().As<IServiceConfigurator>();
            }

            builder.RegisterType<EnvironmentVariableReader>().As<IEnvironmentVariableReader>();

            builder.RegisterType<PersistedApplicationInstanceStore>()
                .WithParameter("startUpInstanceRequest", startUpInstanceRequest)
                .As<IPersistedApplicationInstanceStore>()
                .As<IApplicationInstanceStrategy>();

            builder.RegisterType<EnvFileLocator>().As<IEnvFileLocator>().SingleInstance();
            builder.RegisterType<EnvFileInstanceStrategy>()
                .WithParameter("startUpInstanceRequest", startUpInstanceRequest)
                .As<IApplicationInstanceStrategy>();

            builder.RegisterType<EnvironmentInstanceStrategy>()
                .WithParameter("startUpInstanceRequest", startUpInstanceRequest)
                .As<IApplicationInstanceStrategy>();

            builder.RegisterType<ConfigFileInstanceStrategy>()
                .WithParameter("startUpInstanceRequest", startUpInstanceRequest)
                .As<IApplicationInstanceStrategy>();

            builder.RegisterType<ApplicationInstanceSelector>()
                .WithParameter("startUpInstanceRequest", startUpInstanceRequest)
                .As<IApplicationInstanceSelector>()
                .SingleInstance();

            builder.Register(c =>
            {
                var selector = c.Resolve<IApplicationInstanceSelector>();
                return selector.GetCurrentInstance().Configuration;
            }).As<IKeyValueStore>().SingleInstance();

            builder.RegisterType<HomeConfiguration>()
                .WithParameter("application", startUpInstanceRequest.ApplicationName)
                .As<IHomeConfiguration>()
                .SingleInstance();

            builder.RegisterType<LoggingConfiguration>().As<ILoggingConfiguration>().SingleInstance();
            builder.RegisterType<ProxyConfigParser>().As<IProxyConfigParser>();
            builder.RegisterType<ProxyConfiguration>().As<IProxyConfiguration>();
            builder.RegisterType<ProxyInitializer>().As<IProxyInitializer>().SingleInstance();
            RegisterWatchdog(builder);
        }

        private void RegisterWatchdog(ContainerBuilder builder)
        {
            if (PlatformDetection.IsRunningOnWindows)
            {
                builder.RegisterType<Watchdog>().As<IWatchdog>()
                    .WithParameter("applicationName", startUpInstanceRequest.ApplicationName);
            }
            else
            {
                builder.RegisterType<NullWatchdog>().As<IWatchdog>();
            }
        }
    }
}