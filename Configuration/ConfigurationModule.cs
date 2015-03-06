using System;
using Autofac;
using Octopus.Shared.Diagnostics;
using Octopus.Shared.Security.MasterKey;
using Octopus.Shared.Util;

namespace Octopus.Shared.Configuration
{
    public class ConfigurationModule : Module
    {
        readonly ApplicationName applicationName;
        
        public ConfigurationModule(ApplicationName applicationName)
        {
            this.applicationName = applicationName;
        }

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<ApplicationInstanceStore>().As<IApplicationInstanceStore>();
            builder.Register(c => new ApplicationInstanceSelector(applicationName, c.Resolve<IOctopusFileSystem>(), c.Resolve<IApplicationInstanceStore>(), c.Resolve<ILog>()))
                .As<IApplicationInstanceSelector>().SingleInstance();

            builder.Register(c =>
            {
                var selector = c.Resolve<IApplicationInstanceSelector>();
                if (selector.Current == null)
                    selector.LoadDefaultInstance();

                return selector.Current.Configuration;
            }).As<IKeyValueStore>().SingleInstance();
            builder.RegisterType<UpgradeCheckConfiguration>().As<IUpgradeCheckConfiguration>().SingleInstance();
            builder.Register(c => new HomeConfiguration(applicationName, c.Resolve<IKeyValueStore>())).As<IHomeConfiguration>().SingleInstance();
            builder.RegisterType<LoggingConfiguration>().As<ILoggingConfiguration>().SingleInstance();
            builder.RegisterType<LogInitializer>().As<IStartable>();
            builder.RegisterType<ProxyConfiguration>().As<IProxyConfiguration>();
            builder.RegisterType<ProxyInitializer>().As<IStartable>();
            builder.RegisterType<CommunicationsConfiguration>().As<ICommunicationsConfiguration, ITcpServerCommunicationsConfiguration>().SingleInstance();
            builder.RegisterType<FileStorageConfiguration>().As<IFileStorageConfiguration>().SingleInstance();
            builder.Register(c => new StoredMasterKeyEncryption(c.Resolve<IMasterKeyConfiguration>().MasterKey)).As<IMasterKeyEncryption>().SingleInstance();
        }
    }
}