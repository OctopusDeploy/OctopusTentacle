using System;
using System.IO;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Support.TestAttributes;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    [LinuxTest]
    public class CreateInstanceCommandFixture : CommandFixture<CreateInstanceCommand>
    {
        readonly ITentacleConfigurationUpgrade configurationUpgrade = Substitute.For<ITentacleConfigurationUpgrade>();

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            Command = new CreateInstanceCommand(Substitute.For<IApplicationInstanceManager>(), Substitute.For<ILogFileOnlyLogger>(), new Lazy<ITentacleConfigurationUpgrade>(() => configurationUpgrade));
        }

        [Test]
        public void ANamedInstanceFailsWhenItsKeyCannotBeCreated()
        {
            configurationUpgrade.When(u => u.PrepareKey()).Do(_ => throw new IOException("Permission denied"));

            Action start = () => Start("--instance=Tentacle", "--config=/etc/octopus/tentacle.config");

            start.Should().Throw<IOException>();
        }
    }
}
