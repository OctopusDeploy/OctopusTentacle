using System;
using System.IO;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Internals.Options;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Support.TestAttributes;

namespace Octopus.Tentacle.Tests.Commands
{
    [LinuxOrMacTest]
    public class CreateInstanceCommandTests
    {
        [Test]
        public void ANamedInstanceFailsWhenItsKeyCannotBeCreated()
        {
            var configurationUpgrade = Substitute.For<ITentacleConfigurationUpgrade>();
            configurationUpgrade.When(u => u.PrepareKey()).Do(_ => throw new IOException("Permission denied"));
            var command = new CreateInstanceCommand(
                Substitute.For<IApplicationInstanceManager>(),
                Substitute.For<ILogFileOnlyLogger>(),
                new Lazy<ITentacleConfigurationUpgrade>(() => configurationUpgrade));

            Action start = () => command.Start(
                new[] { "--instance=Tentacle", "--config=/etc/octopus/tentacle.config" },
                Substitute.For<ICommandRuntime>(),
                new OptionSet());

            start.Should().Throw<IOException>();
        }
    }
}
