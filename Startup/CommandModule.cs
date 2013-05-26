using System;
using Autofac;
using Autofac.Integration.Mef;

namespace Octopus.Shared.Startup
{
    public class CommandModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterMetadataRegistrationSources();
            builder.RegisterCommand<HelpCommand>("help", "Prints this help text", "h", "?");
            builder.RegisterType<CommandLocator>().As<ICommandLocator>();
            builder.RegisterType<ServiceInstaller>().As<IServiceInstaller>();
        }
    }
}