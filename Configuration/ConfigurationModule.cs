using System;
using Autofac;
using Octopus.Platform.Deployment.Configuration;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Security.MasterKey;
using Octopus.Shared.Security.MasterKey;

namespace Octopus.Shared.Configuration
{
    public class ConfigurationModule : Module
    {
        readonly ApplicationName applicationName;
        readonly Func<IComponentContext, IKeyValueStore> configurationProvider;
        
        private ConfigurationModule(ApplicationName applicationName, Func<IComponentContext, IKeyValueStore> configurationProvider)
        {
            this.applicationName = applicationName;
            this.configurationProvider = configurationProvider;
        }

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.Register(configurationProvider).As<IKeyValueStore>().SingleInstance();
            builder.RegisterType<UpgradeCheckConfiguration>().As<IUpgradeCheckConfiguration>().SingleInstance();
            builder.Register(c => new HomeConfiguration(applicationName, c.Resolve<IKeyValueStore>())).As<IHomeConfiguration>().As<IStartable>().SingleInstance();
            builder.RegisterType<LoggingConfiguration>().As<ILoggingConfiguration>().As<IStartable>().SingleInstance();
            builder.RegisterType<ProxyConfiguration>().As<IProxyConfiguration>().As<IStartable>().SingleInstance();
            builder.RegisterType<CommunicationsConfiguration>().As<ICommunicationsConfiguration, ITcpServerCommunicationsConfiguration>().SingleInstance();
            builder.RegisterType<FileStorageConfiguration>().As<IFileStorageConfiguration>().SingleInstance();
            builder.RegisterType<StoredMasterKeyEncryption>().As<IMasterKeyEncryption>();
        }

        public static ConfigurationModule FromFile(ApplicationName application, string filePath)
        {
            return new ConfigurationModule(application, c => new XmlFileKeyValueStore(filePath, c.Resolve<ILog>()));
        }

        public static ConfigurationModule FromStore(ApplicationName application, IKeyValueStore store)
        {
            return new ConfigurationModule(application, c => store);
        }

        public static ConfigurationModule FromRegistry(ApplicationName application)
        {
            return new ConfigurationModule(application, c => new WindowsRegistryKeyValueStore(c.Resolve<ILog>()));
        }
    }
}