using System;
using Autofac;

namespace Octopus.Shared.Configuration
{
    public class ConfigurationModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<WindowsRegistry>().As<IWindowsRegistry>();
            builder.RegisterType<OctopusConfiguration>().As<IOctopusConfiguration>();
            builder.RegisterType<TentacleConfiguration>().As<ITentacleConfiguration>();
            builder.RegisterType<ProxyConfiguration>().As<IProxyConfiguration>().As<IStartable>();
        }
    }
}