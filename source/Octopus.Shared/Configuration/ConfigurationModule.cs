using System;
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

            builder.RegisterInstance(startUpInstanceRequest).As<StartUpInstanceRequest>();
            builder.Register(_ => startUpInstanceRequest.ApplicationName).As<ApplicationName>();

            if (PlatformDetection.IsRunningOnWindows)
            {
                builder.RegisterType<WindowsRegistryApplicationInstanceStore>()
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
                .As<IApplicationInstanceStore>();

            builder.RegisterType<EnvFileLocator>().As<IEnvFileLocator>().SingleInstance();
            builder.RegisterType<EnvFileConfigurationStrategy>()
                .As<IApplicationConfigurationStrategy>();

            builder.RegisterType<EnvironmentConfigurationStrategy>()
                .As<IApplicationConfigurationStrategy>();

            builder.RegisterType<ApplicationInstanceSelector>()
                .As<IApplicationInstanceSelector>()
                .SingleInstance();

            builder.RegisterType<WindowsLocalAdminRightsChecker>()
                .As<IWindowsLocalAdminRightsChecker>()
                .SingleInstance();

            builder.RegisterType<ApplicationInstanceManager>()
                .As<IApplicationInstanceManager>()
                .SingleInstance();

            builder.Register(c =>
                {
                    var selector = c.Resolve<IApplicationInstanceSelector>();
                    return selector.Current.Configuration;
                })
                .As<IKeyValueStore>()
                .SingleInstance();

            builder.Register(c =>
                {
                    var selector = c.Resolve<IApplicationInstanceSelector>();
                    return selector.Current.WritableConfiguration;
                })
                .As<IWritableKeyValueStore>()
                .SingleInstance();

            builder.RegisterType<HomeConfiguration>()
                .As<IHomeConfiguration>()
                .SingleInstance();
            builder.RegisterType<WritableHomeConfiguration>()
                .As<IWritableHomeConfiguration>()
                .SingleInstance();

            builder.RegisterType<LoggingConfiguration>().As<ILoggingConfiguration>().SingleInstance();
            builder.RegisterType<ProxyConfigParser>().As<IProxyConfigParser>();
            builder.RegisterType<ProxyConfiguration>().As<IProxyConfiguration>();
            builder.RegisterType<WritableProxyConfiguration>().As<IWritableProxyConfiguration>();
            builder.RegisterType<ProxyInitializer>().As<IProxyInitializer>().SingleInstance();
            RegisterWatchdog(builder);
        }

        void RegisterWatchdog(ContainerBuilder builder)
        {
            if (PlatformDetection.IsRunningOnWindows)
                builder.RegisterType<Watchdog>().As<IWatchdog>();
            else
                builder.RegisterType<NullWatchdog>().As<IWatchdog>();
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

            var startUpInstanceRequest = new StartUpDynamicInstanceRequest(applicationName);
            builder.RegisterInstance(startUpInstanceRequest).As<StartUpInstanceRequest>();
            builder.Register(_ => startUpInstanceRequest.ApplicationName).As<ApplicationName>();

            // the Wpf apps only run on Windows
            builder.RegisterType<WindowsRegistryApplicationInstanceStore>()
                .As<IRegistryApplicationInstanceStore>();
            builder.RegisterType<WindowsServiceConfigurator>().As<IServiceConfigurator>();

            builder.RegisterType<ApplicationInstanceStore>()
                .As<IApplicationInstanceStore>();
            
            builder.RegisterType<ApplicationInstanceManager>()
                .As<IApplicationInstanceManager>();
        }
    }
}