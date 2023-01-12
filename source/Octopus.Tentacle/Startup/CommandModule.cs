using System;
using Autofac;

namespace Octopus.Tentacle.Startup
{
    public class CommandModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterCommand<HelpCommand>("help", "Prints this help text", "h", "?");
            builder.RegisterType<CommandLocator>().As<ICommandLocator>().SingleInstance();
        }
    }
}