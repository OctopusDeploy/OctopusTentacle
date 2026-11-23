#if NET
using System;
using System.IO;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Commands;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Tentacle.Core.Util;
using Octopus.Tentacle.Internals.Options;
using Octopus.Tentacle.Startup;
using Octopus.Tentacle.Tests.Support.TestAttributes;
using Octopus.Tentacle.Tests.Util;
using Octopus.Tentacle.Util;

namespace Octopus.Tentacle.Tests.Commands
{
    [LinuxOrMacTest]
    [NonParallelizable]
    public class CreateInstanceCommandTests
    {
        [Test]
        public void WhenTheConfigurationEncryptionKeyCannotBeCreatedThenNoInstanceIsCreated()
        {
            // Arrange
            var directory = Path.Combine(Path.GetTempPath(), "octopus-create-instance-" + Guid.NewGuid());
            Directory.CreateDirectory(directory);
            var blockingFile = Path.Combine(directory, "not-a-directory");
            File.WriteAllText(blockingFile, "");
            using var _ = new TemporaryEnvironmentVariable(EnvironmentVariables.TentacleInstancesDataDirectory, blockingFile);
            var configurationFile = Path.Combine(directory, "Tentacle", "tentacle.config");
            var log = Substitute.For<ISystemLog>();
            var fileSystem = new OctopusPhysicalFileSystem(log);
            var instanceStore = Substitute.For<IApplicationInstanceStore>();
            var manager = new ApplicationInstanceManager(
                ApplicationName.Tentacle,
                new StartUpRegistryInstanceRequest("Tentacle"),
                fileSystem,
                log,
                instanceStore,
                new Lazy<IWritableHomeConfiguration>(() => Substitute.For<IWritableHomeConfiguration>()));
            var command = new CreateInstanceCommand(manager, Substitute.For<ILogFileOnlyLogger>(), new Lazy<ITentacleConfigurationUpgrade>(() => Substitute.For<ITentacleConfigurationUpgrade>()));

            try
            {
                // Act
                Action create = () => command.Start(new[] { "--instance=Tentacle", $"--config={configurationFile}" }, Substitute.For<ICommandRuntime>(), new OptionSet());

                // Assert
                create.Should().Throw<ControlledFailureException>().WithMessage("*so the instance was not created*");
                File.Exists(configurationFile).Should().BeFalse();
                instanceStore.DidNotReceiveWithAnyArgs().RegisterInstance(default!);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
#endif
