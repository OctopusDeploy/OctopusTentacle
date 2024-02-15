using System;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Manager.Tentacle.Proxy;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Manager.Tentacle.Tests.Utils;
using Octopus.Manager.Tentacle.Util;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;

namespace Octopus.Manager.Tentacle.Tests.WizardFixtures
{
    [TestFixture]
    public class ProxyWizardFixture
    {
        [Test]
        public void WhenInstanceHasStopped_ConfigureProxyScriptShouldBeGeneratedCorrectly()
        {
            // Arrange
            var model = CreateTestProxyWizardModel();
            model.ToggleService = false;

            // Act
            var script = model.GenerateScript().ToCommandLineString();

            // Assert
            var pathToTentacleExe = $"\"{CommandLine.PathToTentacleExe()}\"";
            var expectedOutput = $"{pathToTentacleExe} proxy --instance \"{model.InstanceName}\" --proxyEnable \"True\" --proxyUsername \"\" --proxyPassword \"\" --proxyHost \"\" --proxyPort \"\"";
            script.Should().Be(expectedOutput);
        }
        
        [Test]
        public void WhenInstanceHasNotStopped_ConfigureProxyScriptShouldBeGeneratedCorrectly()
        {
            // Arrange
            var model = CreateTestProxyWizardModel();
            model.ToggleService = true;

            // Act
            var script = model.GenerateScript().ToCommandLineString();

            // Assert
            var pathToTentacleExe = $"\"{CommandLine.PathToTentacleExe()}\"";
            var expectedOutput = $"{pathToTentacleExe} service --instance \"{model.InstanceName}\" --stop" +
                Environment.NewLine + $"{pathToTentacleExe} proxy --instance \"{model.InstanceName}\" --proxyEnable \"True\" --proxyUsername \"\" --proxyPassword \"\" --proxyHost \"\" --proxyPort \"\"" +
                Environment.NewLine + $"{pathToTentacleExe} service --instance \"{model.InstanceName}\" --start";
            script.Should().Be(expectedOutput);
        }

        static ProxyWizardModel CreateTestProxyWizardModel()
        {
            var instanceStore = Substitute.For<IApplicationInstanceStore>();
            var instanceSelectionModel = new InstanceSelectionModel(ApplicationName.Tentacle, instanceStore);
            const string tentacleInstanceName = "TestInstance";
            instanceSelectionModel.New(tentacleInstanceName);

            var model = new ProxyWizardModel(instanceSelectionModel);
            return model;
        }
    }
}