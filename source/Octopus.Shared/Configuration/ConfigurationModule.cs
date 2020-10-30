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
                    .WithParameter(new TypedParameter(typeof(StartUpInstanceRequest), startUpInstanceRequest))
                    .As<IRegistryApplicationInstanceStore>();
                builder.RegisterType<WindowsServiceConfigurator>().As<IServiceConfigurator>();
            }
            else
            {
                builder.RegisterType<NullRegistryApplicationInstanceStore>().As<IRegistryApplicationInstanceStore>();
                builder.RegisterType<LinuxServiceConfigurator>().As<IServiceConfigurator>();
            }

            builder.RegisterType<EnvironmentVariableReader>().As<IEnvironmentVariableReader>();

            builder.RegisterType<ApplicationInstanceStore>()
                .WithParameter(new TypedParameter(typeof(StartUpInstanceRequest), startUpInstanceRequest))
                .As<IApplicationInstanceStore>()
                .As<IApplicationConfigurationStrategy>();

            builder.RegisterType<EnvFileLocator>().As<IEnvFileLocator>().SingleInstance();
            builder.RegisterType<EnvFileConfigurationStrategy>()
                .WithParameter(new TypedParameter(typeof(StartUpInstanceRequest), startUpInstanceRequest))
                .As<IApplicationConfigurationStrategy>();

            builder.RegisterType<EnvironmentConfigurationStrategy>()
                .WithParameter(new TypedParameter(typeof(StartUpInstanceRequest), startUpInstanceRequest))
                .As<IApplicationConfigurationStrategy>();

            builder.RegisterType<ConfigFileConfigurationStrategy>()
                .WithParameter(new TypedParameter(typeof(StartUpInstanceRequest), startUpInstanceRequest))
                .As<IApplicationConfigurationStrategy>();

            builder.RegisterType<ApplicationInstanceSelector>()
                .WithParameter(new TypedParameter(typeof(StartUpInstanceRequest), startUpInstanceRequest))
                .As<IApplicationInstanceSelector>()
                .SingleInstance();

            builder.RegisterType<ApplicationInstanceLocator>()
                .WithParameter(new TypedParameter(typeof(ApplicationName), startUpInstanceRequest.ApplicationName))
                .As<IApplicationInstanceLocator>()
                .SingleInstance();

            builder.RegisterType<ApplicationInstanceManager>()
                .WithParameter(new TypedParameter(typeof(ApplicationName), startUpInstanceRequest.ApplicationName))
                .As<IApplicationInstanceManager>()
                .SingleInstance();

            builder.Register(c =>
            {
                var selector = c.Resolve<IApplicationInstanceSelector>();
                return selector.GetCurrentConfiguration();
            }).As<IKeyValueStore>().SingleInstance();

            builder.Register(c =>
            {
                var selector = c.Resolve<IApplicationInstanceSelector>();
                return selector.GetWritableCurrentConfiguration();
            }).As<IWritableKeyValueStore>().SingleInstance();

            builder.RegisterType<HomeConfiguration>()
                .WithParameter(new TypedParameter(typeof(ApplicationName), startUpInstanceRequest.ApplicationName))
                .As<IHomeConfiguration>()
                .SingleInstance();
            builder.RegisterType<WritableHomeConfiguration>()
                .WithParameter(new TypedParameter(typeof(ApplicationName), startUpInstanceRequest.ApplicationName))
                .As<IWritableHomeConfiguration>()
                .SingleInstance();

            builder.RegisterType<LoggingConfiguration>().As<ILoggingConfiguration>().SingleInstance();
            builder.RegisterType<ProxyConfigParser>().As<IProxyConfigParser>();
            builder.RegisterType<ProxyConfiguration>().As<IProxyConfiguration>();
            builder.RegisterType<WritableProxyConfiguration>().As<IWritableProxyConfiguration>();
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

    /// <summary>
    /// This is for use in the Octopus Server Manager and Tentacle Manager WPF applications as a stop-gap
    /// Long-term, we want these applications to stand alone without referencing Octopus.Shared or Octopus.Core and instead use the CLI API.
    /// TODO: Remove this once the WPF applications no longer need runtime access to these classes.
    /// </summary>
    public class ManagerConfigurationModule : Module
    {
        readonly ApplicationName applicationName;

        public ManagerConfigurationModule(ApplicationName applicationName)
        {
            this.applicationName = applicationName;
        }

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // the Wpf apps only run on Windows
            builder.RegisterType<RegistryApplicationInstanceStore>()
                .WithParameter(new TypedParameter(typeof(ApplicationName), applicationName))
                .As<IRegistryApplicationInstanceStore>();
            builder.RegisterType<WindowsServiceConfigurator>().As<IServiceConfigurator>();

            builder.RegisterType<ApplicationInstanceLocator>()
                .WithParameter(new TypedParameter(typeof(ApplicationName), applicationName))
                .As<IApplicationInstanceLocator>();

            builder.RegisterType<ApplicationInstanceManager>()
                .WithParameter(new TypedParameter(typeof(ApplicationName), applicationName))
                .As<IApplicationInstanceManager>();
        }
    }
}