using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Internals.Options;
using Octopus.Tentacle.Startup;

namespace Octopus.Tentacle.Tests.Commands
{
    public class CommandFixture<TCommand> where TCommand : ICommand
    {
        protected TCommand Command { get; set; } = default!;

        [SetUp]
        public virtual void SetUp()
        {
        }

        protected void Start(params string[] args)
        {
            Command.Start(args, Substitute.For<ICommandRuntime>(), new OptionSet());
        }

        protected void Stop()
        {
            Command.Stop();
        }
    }
}