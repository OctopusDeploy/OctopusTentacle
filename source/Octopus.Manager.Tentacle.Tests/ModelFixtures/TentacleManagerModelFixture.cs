using System;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Manager.Tentacle.Controls;
using Octopus.Manager.Tentacle.DeleteWizard;
using Octopus.Manager.Tentacle.Proxy;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard;
using Octopus.Manager.Tentacle.TentacleConfiguration.TentacleManager;
using Octopus.Manager.Tentacle.Tests.Utils;
using Octopus.Manager.Tentacle.Util;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;
using Octopus.Tentacle.Util;

namespace Octopus.Manager.Tentacle.Tests.ModelFixtures
{
    [TestFixture]
    public class TentacleManagerModelFixture
    {
        static readonly string CommandLinePath = CommandLine.PathToTentacleExe();
        
        [Test]
        public void WhenStartingATentacle_ScriptShouldBeGeneratedCorrectly()
        {
            // Arrange
            var model = CreateTestTentacleManagerViewModel();
            
            // Act
            var script = model.ServiceWatcher.GetStartCommands().ToCommandLineString();

            // Assert
            var pathToTentacleExe = $"\"{CommandLinePath}\"";
            var expectedOutput = $"{pathToTentacleExe} service --instance \"{model.InstanceName}\" --start";
            script.Should().Be(expectedOutput);
        }
        
        [Test]
        public void WhenStoppingATentacle_ScriptShouldBeGeneratedCorrectly()
        {
            // Arrange
            var model = CreateTestTentacleManagerViewModel();
            
            // Act
            var script = model.ServiceWatcher.GetStopCommands().ToCommandLineString();

            // Assert
            var pathToTentacleExe = $"\"{CommandLinePath}\"";
            var expectedOutput = $"{pathToTentacleExe} service --instance \"{model.InstanceName}\" --stop";
            script.Should().Be(expectedOutput);
        }

        [Test]
        public void WhenReinstallingATentacle_ScriptShouldBeGeneratedCorrectly()
        {
            // Arrange
            var model = CreateTestTentacleManagerViewModel();
            
            // Act
            var script = model.ServiceWatcher.GetRepairCommands().ToCommandLineString();

            // Assert
            var pathToTentacleExe = $"\"{CommandLinePath}\"";
            var expectedOutput = $"{pathToTentacleExe} service --instance \"{model.InstanceName}\" --stop" +
                Environment.NewLine + $"{pathToTentacleExe} service --instance \"{model.InstanceName}\" --uninstall" +
                Environment.NewLine + $"{pathToTentacleExe} service --instance \"{model.InstanceName}\" --install --start";
            script.Should().Be(expectedOutput);
        }
        
        [Test]
        public void WhenReconfiguringATentacle_ScriptShouldBeGeneratedCorrectly()
        {
            // Arrange
            var model = CreateTestTentacleManagerViewModel();
            
            // Act
            var script = model.ServiceWatcher.GetReconfigureCommands().ToCommandLineString();

            // Assert
            var pathToTentacleExe = $"\"{CommandLinePath}\"";
            var expectedOutput = $"{pathToTentacleExe} service --instance \"{model.InstanceName}\" --stop" +
                Environment.NewLine + $"{pathToTentacleExe} service --instance \"{model.InstanceName}\" --install --start";
            script.Should().Be(expectedOutput);
        }
        
        static TentacleManagerModel CreateTestTentacleManagerViewModel()
        {
            var instanceStore = Substitute.For<IApplicationInstanceStore>();
            var instanceSelectionModel = new InstanceSelectionModel(ApplicationName.Tentacle, instanceStore);
            const string tentacleInstanceName = "TestInstance";
            instanceSelectionModel.New(tentacleInstanceName);

            var model = new TentacleManagerModel(
                Substitute.For<IOctopusFileSystem>(),
                Substitute.For<IApplicationInstanceSelector>(),
                Substitute.For<ICommandLineRunner>(),
                instanceSelectionModel,
                Substitute.For<Func<DeleteWizardModel>>(),
                Substitute.For<Func<ProxyWizardModel>>(),
                Substitute.For<Func<PollingProxyWizardModel>>(),
                Substitute.For<Func<SetupTentacleWizardModel>>()
            );

            model.InstanceName = tentacleInstanceName;
            var serviceWatcher = new ServiceWatcher(ApplicationName.Tentacle, tentacleInstanceName, CommandLinePath);
            model.ServiceWatcher = serviceWatcher;
            
            return model;
        }
    }
}