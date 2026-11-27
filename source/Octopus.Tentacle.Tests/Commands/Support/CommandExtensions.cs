using NSubstitute;
using Octopus.Tentacle.Internals.Options;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Tests.Commands.Support
{
    public static class CommandExtensions
    {
        public static void StartWith(this ICommand command, params string[] args)
        {
            command.Start(args, Substitute.For<ICommandRuntime>(), new OptionSet());
        }
    }
}
