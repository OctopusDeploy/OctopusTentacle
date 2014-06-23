using System;
using Autofac;
using Autofac.Builder;

namespace Octopus.Shared.Startup
{
    public static class AutofacExtensions
    {
        public static IRegistrationBuilder<TCommand, ConcreteReflectionActivatorData, SingleRegistrationStyle> RegisterCommand<TCommand>(this ContainerBuilder builder, string name, string description, params string[] aliases)
            where TCommand : ICommand
        {
            return builder.RegisterType<TCommand>()
                .As<ICommand>()
                .WithMetadata<CommandMetadata>(m => m
                    .For(x => x.Name, name)
                    .For(x => x.Aliases, aliases)
                    .For(x => x.Description, description));
        }
    }
}