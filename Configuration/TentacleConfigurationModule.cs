using System;
using Autofac;
using Octopus.Shared.Diagnostics;

namespace Octopus.Shared.Configuration
{
    public class TentacleConfigurationModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<WindowsRegistry>().As<IWindowsRegistry>();
            builder.RegisterType<ProxyConfiguration>().As<IProxyConfiguration>().As<IStartable>();
            builder.RegisterType<RegistryTentacleConfiguration>().As<ITentacleConfiguration>();
        }
    }
}