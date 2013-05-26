using System;
using Autofac;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Configuration
{
    public class ConfigurationModule : Module
    {
        readonly Func<IComponentContext, IKeyValueStore> configurationProvider;
        
        private ConfigurationModule(Func<IComponentContext, IKeyValueStore> configurationProvider)
        {
            this.configurationProvider = configurationProvider;
        }

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.Register(configurationProvider).As<IKeyValueStore>().SingleInstance();
            builder.RegisterType<DeploymentProcessConfiguration>().As<IDeploymentProcessConfiguration>().SingleInstance();
            builder.RegisterType<WebPortalConfiguration>().As<IWebPortalConfiguration>().SingleInstance();
            builder.RegisterType<UpgradeCheckConfiguration>().As<IUpgradeCheckConfiguration>().SingleInstance();
            builder.RegisterType<HomeConfiguration>().As<IHomeConfiguration>().As<IStartable>().SingleInstance();
            builder.RegisterType<OctopusServerStorageConfiguration>().As<IOctopusServerStorageConfiguration>();
            builder.RegisterType<LoggingConfiguration>().As<ILoggingConfiguration>().As<IStartable>().SingleInstance();
            builder.RegisterType<ProxyConfiguration>().As<IProxyConfiguration>().As<IStartable>().SingleInstance();
            builder.RegisterType<RegistryTentacleConfiguration>().As<ITentacleConfiguration>().SingleInstance();
        }

        public static ConfigurationModule FromFile(string filePath)
        {
            return new ConfigurationModule(c => new XmlFileKeyValueStore(filePath, c.Resolve<ILog>()));
        }

        public static ConfigurationModule FromStore(IKeyValueStore store)
        {
            return new ConfigurationModule(c => store);
        }

        public static ConfigurationModule FromRegistry()
        {
            return new ConfigurationModule(c => new WindowsRegistryKeyValueStore(c.Resolve<ILog>()));
        }
    }
}