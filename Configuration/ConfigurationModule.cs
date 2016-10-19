using Autofac;
using Octopus.Server.Extensibility.HostServices.Configuration;

namespace Octopus.Shared.Configuration
{
    public class ConfigurationModule : Module
    {
        readonly ApplicationName applicationName;
        readonly string instanceName;

        public ConfigurationModule(ApplicationName applicationName, string instanceName)
        {
            this.applicationName = applicationName;
            this.instanceName = instanceName;
        }

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            
            builder.RegisterType<ApplicationInstanceStore>().As<IApplicationInstanceStore>();
            builder.RegisterType<ApplicationInstanceSelector>()
                .WithParameter("applicationName", applicationName)
                .WithParameter("instanceName", instanceName)
                .As<IApplicationInstanceSelector>()
                .SingleInstance();
            builder.Register(c =>
            {
                var selector = c.Resolve<IApplicationInstanceSelector>();
                return selector.Current.Configuration;
            }).As<IKeyValueStore>();
            builder.RegisterType<UpgradeCheckConfiguration>().As<IUpgradeCheckConfiguration>().SingleInstance();
            builder.RegisterType<HomeConfiguration>()
                .WithParameter("application", applicationName)
                .As<IHomeConfiguration>()
                .SingleInstance();
            builder.RegisterType<LoggingConfiguration>().As<ILoggingConfiguration>().SingleInstance();
            builder.RegisterType<LogInitializer>().As<ILogInitializer>();
            builder.RegisterType<ProxyConfigParser>().As<IProxyConfigParser>();
            builder.RegisterType<PollingProxyConfiguration>().As<IPollingProxyConfiguration>();
            builder.RegisterType<ProxyConfiguration>().As<IProxyConfiguration>();
            builder.RegisterType<ProxyInitializer>().As<IStartable>();
        }
    }
}