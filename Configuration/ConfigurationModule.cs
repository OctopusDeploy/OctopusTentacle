using System;
using Autofac;

namespace Octopus.Shared.Configuration
{
    public class ConfigurationModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<RegistryGlobalConfiguration>().As<IGlobalConfiguration>();
        }
    }
}