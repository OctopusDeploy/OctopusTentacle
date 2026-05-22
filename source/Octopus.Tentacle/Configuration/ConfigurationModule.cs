using System;
using System.Threading;
using Autofac;
using Octopus.Tentacle.Configuration.Crypto;
using Octopus.Tentacle.Configuration.EnvironmentVariableMappings;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Configuration;
using Octopus.Tentacle.Kubernetes;
using Octopus.Tentacle.Kubernetes.Configuration;
using Octopus.Tentacle.Kubernetes.Crypto;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Util;
using Octopus.Tentacle.Watchdog;

namespace Octopus.Tentacle.Configuration
{
    public class ConfigurationModule : Module
    {
        private readonly ApplicationName applicationName;
        readonly StartUpInstanceRequest startUpInstanceRequest;

        public ConfigurationModule(ApplicationName applicationName,StartUpInstanceRequest startUpInstanceRequest)
        {
            this.applicationName = applicationName;
            this.startUpInstanceRequest = startUpInstanceRequest;
        }

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterInstance(startUpInstanceRequest).As<StartUpInstanceRequest>();
            builder.Register(_ => applicationName).As<ApplicationName>();
            builder.Register((c) => c.Resolve<IApplicationInstanceSelector>().Current).AsSelf();

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
            builder.RegisterType<EnvFileConfigurationContributor>()
                .As<IApplicationConfigurationContributor>();

            builder.RegisterType<EnvironmentConfigurationContributor>()
                .As<IApplicationConfigurationContributor>();

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
                .As<IHomeDirectoryProvider>()
                .SingleInstance();
            builder.RegisterType<WritableHomeConfiguration>()
                .As<IWritableHomeConfiguration>()
                .SingleInstance();

            builder.RegisterType<LogInitializer>().AsSelf();
            builder.RegisterType<LoggingConfiguration>().As<ILoggingConfiguration>().SingleInstance();
            builder.RegisterType<ProxyConfigParser>().As<IProxyConfigParser>();
            builder.RegisterType<ProxyConfiguration>().As<IProxyConfiguration>();
            builder.RegisterType<WritableProxyConfiguration>().As<IWritableProxyConfiguration>();
            builder.RegisterType<ProxyInitializer>().As<IProxyInitializer>().SingleInstance();
            
            //Even though these are Kubernetes types, we need to include them in this module as they are used lazily in the base types
            builder.Register(c =>
                {
                    var configMapService = c.Resolve<IKubernetesConfigMapService>();
                    var encryptor = c.Resolve<IKubernetesMachineKeyEncryptor>();
                    // We're inside an Autofac sync factory delegate — Autofac's Register() API has no
                    // async overload. Container build happens once at startup on a plain thread-pool
                    // worker with no SynchronizationContext, so blocking with .GetAwaiter().GetResult()
                    // is deadlock-safe here.
                    return ConfigMapKeyValueStore.CreateAsync(configMapService, encryptor, CancellationToken.None).GetAwaiter().GetResult();
                })
                .SingleInstance();
            builder.RegisterType<KubernetesMachineEncryptionKeyProvider>().As<IKubernetesMachineEncryptionKeyProvider>().SingleInstance();
            builder.RegisterType<KubernetesMachineKeyEncryptor>().As<IKubernetesMachineKeyEncryptor>().SingleInstance();
            
            RegisterWatchdog(builder);
        }

        void RegisterWatchdog(ContainerBuilder builder)
        {
            if (PlatformDetection.IsRunningOnWindows)
                builder.RegisterType<Watchdog.Watchdog>().As<IWatchdog>();
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

            builder.RegisterInstance(new StartUpDynamicInstanceRequest()).As<StartUpInstanceRequest>();
            builder.Register(_ => applicationName).As<ApplicationName>();

            // the Wpf apps only run on Windows
            builder.RegisterType<WindowsRegistryApplicationInstanceStore>()
                .As<IRegistryApplicationInstanceStore>();
            builder.RegisterType<WindowsServiceConfigurator>().As<IServiceConfigurator>();

            builder.RegisterType<ApplicationInstanceStore>()
                .As<IApplicationInstanceStore>();

            builder.RegisterType<KubernetesMachineKeyEncryptor>().As<IKubernetesMachineKeyEncryptor>().SingleInstance();
            builder.Register(c =>
                {
                    var configMapService = c.Resolve<IKubernetesConfigMapService>();
                    var encryptor = c.Resolve<IKubernetesMachineKeyEncryptor>();
                    // We're inside an Autofac sync factory delegate — Autofac's Register() API has no
                    // async overload. Container build happens once at startup on a plain thread-pool
                    // worker with no SynchronizationContext, so blocking with .GetAwaiter().GetResult()
                    // is deadlock-safe here.
                    return ConfigMapKeyValueStore.CreateAsync(configMapService, encryptor, CancellationToken.None).GetAwaiter().GetResult();
                })
                .SingleInstance();

            builder.RegisterType<HomeConfiguration>()
                .As<IHomeConfiguration>()
                .As<IHomeDirectoryProvider>()
                .SingleInstance();
            builder.RegisterType<WritableHomeConfiguration>()
                .As<IWritableHomeConfiguration>()
                .SingleInstance();
            
            builder.RegisterType<ApplicationInstanceManager>()
                .As<IApplicationInstanceManager>();
            
            builder.RegisterType<ApplicationInstanceSelector>()
                .As<IApplicationInstanceSelector>();
            
        }
    }
}