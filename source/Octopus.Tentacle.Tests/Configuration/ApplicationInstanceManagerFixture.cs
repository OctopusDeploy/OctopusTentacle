using System;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Tests.Support.TestAttributes;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Configuration
{
    [TestFixture]
    [LinuxTest]
    public class ApplicationInstanceManagerFixture
    {
        [Test]
        public void WhenTheConfigurationEncryptionKeyCannotBeCreatedThenNoInstanceIsCreated()
        {
            // Arrange
            var fileSystem = Substitute.For<IOctopusFileSystem>();
            fileSystem.When(f => f.EnsureDirectoryExists(Arg.Is<string>(d => d.EndsWith("/keys/Tentacle"))))
                .Do(_ => throw new UnauthorizedAccessException("Permission denied"));
            var instanceStore = Substitute.For<IApplicationInstanceStore>();
            var manager = new ApplicationInstanceManager(
                ApplicationName.Tentacle,
                new StartUpRegistryInstanceRequest("Tentacle"),
                fileSystem,
                Substitute.For<ISystemLog>(),
                instanceStore,
                new Lazy<IWritableHomeConfiguration>(() => Substitute.For<IWritableHomeConfiguration>()));

            // Act
            Action create = () => manager.CreateInstance("Tentacle", "/etc/octopus/Tentacle/tentacle.config", "/etc/octopus/Tentacle");

            // Assert
            create.Should().Throw<ControlledFailureException>().WithMessage("*Permission denied*");
            fileSystem.DidNotReceive().EnsureDirectoryExists("/etc/octopus/Tentacle");
            instanceStore.DidNotReceiveWithAnyArgs().RegisterInstance(default!);
        }
    }
}
