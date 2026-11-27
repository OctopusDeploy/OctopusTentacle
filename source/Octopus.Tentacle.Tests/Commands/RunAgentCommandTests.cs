using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Background;
using Octopus.Tentacle.Communications;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Commands.Support;
using Octopus.Time;

namespace Octopus.Tentacle.Tests.Commands
{
    public class RunAgentCommandTests
    {
        [Test]
        public void WhenCommandIsStartedThenBackgroundServicesAreStarted()
        {
            var halibut = Substitute.For<IHalibutInitializer>();
            var backgroundTasks = new[] { Substitute.For<IBackgroundTask>(), Substitute.For<IBackgroundTask>() };
            var command = CommandBuilder.RunAgent(halibut, backgroundTasks: backgroundTasks);

            command.StartWith();

            halibut.Received().Start();

            foreach (var backgroundTask in backgroundTasks)
            {
                backgroundTask.Received().Start();
            }
        }

        [Test]
        public void WhenCommandIsStoppedThenBackgroundServicesAreStopped()
        {
            var halibut = Substitute.For<IHalibutInitializer>();
            var backgroundTasks = new[] { Substitute.For<IBackgroundTask>(), Substitute.For<IBackgroundTask>() };
            ICommand command = CommandBuilder.RunAgent(halibut, backgroundTasks: backgroundTasks);

            command.StartWith();

            command.Stop();

            halibut.Received().Stop();

            foreach (var backgroundTask in backgroundTasks)
            {
                backgroundTask.Received().Stop();
            }
        }

        [Test]
        public void WaitsBeforeStarting()
        {
            var halibut = Substitute.For<IHalibutInitializer>();
            var sleep = Substitute.For<ISleep>();
            var command = CommandBuilder.RunAgent(halibut, sleep);

            command.StartWith("/wait=2000");

            sleep.Received().For(2000);

            halibut.Received().Start();
        }
    }
}
