using System;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using NUnit.Framework.Constraints;
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
            var pathToTentacleExe = $"\"{model.Executable}\"";
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
            var pathToTentacleExe = $"\"{model.Executable}\"";
            var expectedOutput = $"{pathToTentacleExe} service --instance \"{model.InstanceName}\" --stop" +
                Environment.NewLine + $"{pathToTentacleExe} proxy --instance \"{model.InstanceName}\" --proxyEnable \"True\" --proxyUsername \"\" --proxyPassword \"\" --proxyHost \"\" --proxyPort \"\"" +
                Environment.NewLine + $"{pathToTentacleExe} service --instance \"{model.InstanceName}\" --start";
            script.Should().Be(expectedOutput);
        }
        
        [Test]
        public void WhenUsingCustomProxyConfig_GeneratedScriptShouldHaveConfiguredArguments()
        {
            // Arrange
            var model = CreateTestProxyWizardModel();
            model.ProxyConfigType = ProxyConfigType.CustomProxy;

            // Act
            var script = model.GenerateScript().ToCommandLineString();
            
            // Assert
            var pathToTentacleExe = $"\"{model.Executable}\"";
            var expectedOutput = $"{pathToTentacleExe} proxy --instance \"{model.InstanceName}\" --proxyEnable \"True\" " +
                $"--proxyUsername \"{model.ProxyUsername}\" --proxyPassword \"{model.ProxyPassword}\" --proxyHost \"{model.ProxyServerHost}\" --proxyPort \"{model.ProxyServerPort}\"";
            script.Should().Be(expectedOutput);
        }
        
        [Test]
        public void WhenUsingNoProxyConfig_GeneratedScriptShouldNotHaveConfiguredArguments()
        {
            // Arrange
            var model = CreateTestProxyWizardModel();
            model.ProxyConfigType = ProxyConfigType.NoProxy;

            // Act
            var script = model.GenerateScript().ToCommandLineString();

            // Assert
            var pathToTentacleExe = $"\"{model.Executable}\"";
            var expectedOutput = $"{pathToTentacleExe} proxy --instance \"{model.InstanceName}\" --proxyEnable \"False\" " +
                "--proxyUsername \"\" --proxyPassword \"\" --proxyHost \"\" --proxyPort \"\"";
            script.Should().Be(expectedOutput);
        }
        
        [Test]
        public void WhenUsingDefaultProxyConfig_GeneratedScriptShouldNotHaveConfiguredArguments()
        {
            // Arrange
            var model = CreateTestProxyWizardModel();
            model.ProxyConfigType = ProxyConfigType.DefaultProxy;

            // Act
            var script = model.GenerateScript().ToCommandLineString();

            // Assert
            var pathToTentacleExe = $"\"{model.Executable}\"";
            var expectedOutput = $"{pathToTentacleExe} proxy --instance \"{model.InstanceName}\" --proxyEnable \"True\" " +
                "--proxyUsername \"\" --proxyPassword \"\" --proxyHost \"\" --proxyPort \"\"";
            script.Should().Be(expectedOutput);
        }

        [Test] public void WhenUsingDefaultProxyCustomCredentialsConfig_GeneratedScriptShouldOnlyHaveUserNameAndPasswordConfigured()
        {
            // Arrange
            var model = CreateTestProxyWizardModel();
            model.ProxyConfigType = ProxyConfigType.DefaultProxyCustomCredentials;

            // Act
            var script = model.GenerateScript().ToCommandLineString();

            // Assert
            var pathToTentacleExe = $"\"{model.Executable}\"";
            var expectedOutput = $"{pathToTentacleExe} proxy --instance \"{model.InstanceName}\" --proxyEnable \"True\" " +
                $"--proxyUsername \"{model.ProxyUsername}\" --proxyPassword \"{model.ProxyPassword}\" --proxyHost \"\" --proxyPort \"\"";
            script.Should().Be(expectedOutput);
        }

        static ProxyWizardModel CreateTestProxyWizardModel()
        {
            var instanceStore = Substitute.For<IApplicationInstanceStore>();
            var instanceSelectionModel = new InstanceSelectionModel(ApplicationName.Tentacle, instanceStore);
            const string tentacleInstanceName = "TestInstance";
            instanceSelectionModel.New(tentacleInstanceName);

            var model = new ProxyWizardModel(instanceSelectionModel)
            {
                ProxyUsername = "TestProxyUserName",
                ProxyPassword = "TestProxyPassword",
                ProxyServerHost = "localhost",
                ProxyServerPort = 8080
            };
            return model;
        }
    }
}