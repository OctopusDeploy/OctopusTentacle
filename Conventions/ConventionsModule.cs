using System;
using Autofac;
using Octopus.Shared.Integration.PowerShell;

namespace Octopus.Shared.Conventions
{
    public class ConventionsModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<PowerShellEngineSelector>().As<IPowerShell>();
            
            builder.RegisterAssemblyTypes(GetType().Assembly)
                .As<IConvention>()
                .PropertiesAutowired();

            builder.RegisterType<ConventionProcessor>().As<IConventionProcessor>();
        }
    }
}