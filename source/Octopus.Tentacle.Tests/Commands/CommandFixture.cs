using NSubstitute;
using NUnit.Framework;
using Octopus.Shared.Internals.Options;
using Octopus.Shared.Startup;

namespace Octopus.Tentacle.Tests.Commands
{
    public class CommandFixture<TCommand> where TCommand : ICommand
    {
        protected TCommand Command { get; set; }

        [SetUp]
        public virtual void SetUp()
        {
        }

        protected void Start(params string[] args)
        {
            Command.Start(args, Substitute.For<ICommandRuntime>(), new OptionSet(), "TestTentacle", "1.0.0", "1.0.0", new string[0], "TestInstance");
        }

        protected void Stop()
        {
            Command.Stop();
        }
    }
}