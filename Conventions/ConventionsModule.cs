using System;
using Autofac;
using Octopus.Shared.Integration.Scripting;

namespace Octopus.Shared.Conventions
{
    public class ConventionsModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ScriptEngineSelector>().As<IScriptRunner>();
            
            builder.RegisterAssemblyTypes(GetType().Assembly)
                .As<IConvention>()
                .PropertiesAutowired();

            builder.RegisterType<ConventionProcessor>().As<IConventionProcessor>();
        }
    }
}