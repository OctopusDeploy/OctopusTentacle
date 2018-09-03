using System;
using System.IO;
using Autofac;
using Octopus.Configuration;
using Octopus.Shared.Services;

namespace Octopus.Shared.Configuration
{
    public class ConfigurationModule : Module
    {
        readonly ApplicationName applicationName;
        readonly string instanceName;
        readonly string machineConfigurationHomeDirectory;

        public ConfigurationModule(string machineConfigurationHomeDirectory, ApplicationName applicationName, string instanceName)
        {
            this.machineConfigurationHomeDirectory = machineConfigurationHomeDirectory;
            if (string.IsNullOrWhiteSpace(this.machineConfigurationHomeDirectory))
                this.machineConfigurationHomeDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Octopus");

            this.applicationName = applicationName;
            this.instanceName = instanceName;
        }

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            
            builder.RegisterType<RegistryApplicationInstanceStore>().As<IRegistryApplicationInstanceStore>();
            builder.RegisterType<ApplicationInstanceStore>()
                .WithParameter("machineConfigurationHomeDirectory", machineConfigurationHomeDirectory)
                .As<IApplicationInstanceStore>();
            builder.RegisterType<ApplicationInstanceSelector>()
                .WithParameter("applicationName", applicationName)
                .WithParameter("currentInstanceName", instanceName)
                .As<IApplicationInstanceSelector>()
                .SingleInstance();

            builder.Register(c =>
            {
                var selector = c.Resolve<IApplicationInstanceSelector>();
                return selector.GetCurrentInstance().Configuration;
            }).As<IKeyValueStore>().SingleInstance();

            builder.RegisterType<HomeConfiguration>()
                .WithParameter("application", applicationName)
                .As<IHomeConfiguration>()
                .SingleInstance();

            builder.RegisterType<BundledPackageStoreConfiguration>().As<IBundledPackageStoreConfiguration>().SingleInstance();
            builder.RegisterType<LoggingConfiguration>().As<ILoggingConfiguration>().SingleInstance();
            builder.RegisterType<LogInitializer>().As<ILogInitializer>();
            builder.RegisterType<ProxyConfigParser>().As<IProxyConfigParser>();
            builder.RegisterType<PollingProxyConfiguration>().As<IPollingProxyConfiguration>();
            builder.RegisterType<ProxyConfiguration>().As<IProxyConfiguration>();
            builder.RegisterType<ProxyInitializer>().As<IProxyInitializer>().SingleInstance();
            builder.RegisterType<Watchdog>().As<IWatchdog>()
                .WithParameter("applicationName", applicationName);
        }
    }
}