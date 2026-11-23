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
using Octopus.Tentacle.Kubernetes.Configuration;
using Octopus.Tentacle.Tests.Support;
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
            using var dataDirectory = new TemporaryEnvironmentVariable(EnvironmentVariables.TentacleInstancesDataDirectory, blockingFile);
            using var homeDirectory = new TemporaryEnvironmentVariable(EnvironmentVariables.TentacleMachineConfigurationHomeDirectory, directory);
            var configurationFile = Path.Combine(directory, "Tentacle", "tentacle.config");
            var log = new InMemoryLog();
            var fileSystem = new OctopusPhysicalFileSystem(log);
            var request = new StartUpRegistryInstanceRequest("Tentacle");
            var instanceStore = new ApplicationInstanceStore(ApplicationName.Tentacle, log, fileSystem, Substitute.For<IRegistryApplicationInstanceStore>());
            var selector = new ApplicationInstanceSelector(ApplicationName.Tentacle, instanceStore, request, Array.Empty<IApplicationConfigurationContributor>(), new Lazy<ConfigMapKeyValueStore>(() => throw new NotSupportedException()), fileSystem, log);
            var manager = new ApplicationInstanceManager(ApplicationName.Tentacle, request, fileSystem, log, instanceStore, new Lazy<IWritableHomeConfiguration>(() => throw new NotSupportedException()));
            var command = new CreateInstanceCommand(manager, Substitute.For<ILogFileOnlyLogger>(), new Lazy<ITentacleConfigurationUpgrade>(() => new TentacleConfigurationUpgrade(selector, fileSystem, log)));

            try
            {
                // Act
                Action create = () => command.Start(new[] { "--instance=Tentacle", $"--config={configurationFile}" }, Substitute.For<ICommandRuntime>(), new OptionSet());

                // Assert
                create.Should().Throw<ControlledFailureException>().WithMessage("*so the instance was not created*");
                File.Exists(configurationFile).Should().BeFalse();
                instanceStore.ListInstances().Should().BeEmpty();
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
#endif
