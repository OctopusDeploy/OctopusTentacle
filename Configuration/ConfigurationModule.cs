using System;
using Autofac;
using Octopus.Platform.Deployment.Configuration;
using Octopus.Platform.Diagnostics;
using Octopus.Platform.Security.MasterKey;
using Octopus.Platform.Util;
using Octopus.Shared.Security.MasterKey;

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
            builder.RegisterType<UpgradeCheckConfiguration>().As<IUpgradeCheckConfiguration>();
            builder.Register(c => new HomeConfiguration(applicationName, c.Resolve<IKeyValueStore>())).As<IHomeConfiguration>();
            builder.RegisterType<LoggingConfiguration>().As<ILoggingConfiguration>();
            builder.RegisterType<LogInitializer>().As<IStartable>();
            builder.RegisterType<ProxyConfiguration>().As<IProxyConfiguration>();
            builder.RegisterType<ProxyInitializer>().As<IStartable>();
            builder.RegisterType<CommunicationsConfiguration>().As<ICommunicationsConfiguration, ITcpServerCommunicationsConfiguration>();
            builder.RegisterType<FileStorageConfiguration>().As<IFileStorageConfiguration>();
            builder.RegisterType<StoredMasterKeyEncryption>().As<IMasterKeyEncryption>();
        }
    }
}