using System;
using System.Text;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Client.Model;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard;
using Octopus.Manager.Tentacle.Tests.Utils;
using Octopus.Manager.Tentacle.Util;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;

namespace Octopus.Manager.Tentacle.Tests.WizardFixtures
{
    public class SetupTentacleWizardFixture
    {
        [Test]
        public void WhenSettingUpListeningTentacle_ScriptShouldBeCorrectlyGenerated()
        {
            // Arrange
            var model = CreateTestSetupTentacleWizardModel();
            model.CommunicationStyle = CommunicationStyle.TentaclePassive;
            model.ListenPort = "10933";
            model.OctopusThumbprint = "TestThumbprint";
            model.FirewallException = true;
            model.FirewallExceptionPossible = true;

            // Act
            var script = model.GenerateScript().ToCommandLineString();

            // Assert
            var pathToTentacleExe = $"\"{model.PathToTentacleExe ?? CommandLine.PathToTentacleExe()}\"";
            var expectedOutput = $"{pathToTentacleExe} create-instance --instance \"{model.InstanceName}\" --config \"C:\\Octopus\\{model.InstanceName}\\Tentacle-{model.InstanceName}.config\"" +
                    Environment.NewLine + $"{pathToTentacleExe} new-certificate --instance \"{model.InstanceName}\" --if-blank" +
                    Environment.NewLine + $"{pathToTentacleExe} configure --instance \"{model.InstanceName}\" --reset-trust" +
                    Environment.NewLine + $"{pathToTentacleExe} configure --instance \"{model.InstanceName}\" --app \"C:\\Octopus\\Applications\\{model.InstanceName}\" --port \"{model.ListenPort}\" --noListen \"False\"" +
                    Environment.NewLine + $"{pathToTentacleExe} configure --instance \"{model.InstanceName}\" --trust \"{model.OctopusThumbprint}\"" +
                    Environment.NewLine + $"\"netsh\" advfirewall firewall add rule \"name=Octopus Deploy Tentacle\" dir=in action=allow protocol=TCP localport={model.ListenPort}" +
                    Environment.NewLine + $"{pathToTentacleExe} service --instance \"{model.InstanceName}\" --install --stop --start";
            script.Should().Be(expectedOutput);
        }
        
        [Test]
        public void WhenSettingUpPollingTentacle_ScriptShouldBeCorrectlyGenerated()
        {
            // Arrange
            var model = CreateTestSetupTentacleWizardModel();
            model.CommunicationStyle = CommunicationStyle.TentacleActive;
            model.OctopusServerUrl = "localhost";
            model.AuthMode = AuthMode.APIKey;
            model.ApiKey = "TestApiKey";
            model.MachineType = MachineType.DeploymentTarget;
            model.MachineName = "TestMachineName";
            model.SelectedSpace = "Default";
            model.HaveCredentialsBeenVerified = true;
            model.ServerCommsPort = "10943";

            // Act
            var script = model.GenerateScript().ToCommandLineString();

            // Assert
            var pathToTentacleExe = $"\"{model.PathToTentacleExe ?? CommandLine.PathToTentacleExe()}\"";
            var expectedOutput = new StringBuilder();
            expectedOutput.AppendLine($"{pathToTentacleExe} create-instance --instance \"{model.InstanceName}\" --config \"C:\\Octopus\\{model.InstanceName}\\Tentacle-{model.InstanceName}.config\"");
            expectedOutput.AppendLine($"{pathToTentacleExe} new-certificate --instance \"{model.InstanceName}\" --if-blank");
            expectedOutput.AppendLine($"{pathToTentacleExe} configure --instance \"{model.InstanceName}\" --reset-trust");
            expectedOutput.AppendLine($"{pathToTentacleExe} configure --instance \"{model.InstanceName}\" --app \"C:\\Octopus\\Applications\\{model.InstanceName}\" --port \"{model.ListenPort}\" --noListen \"True\"");
            expectedOutput.AppendLine($"{pathToTentacleExe} polling-proxy --instance \"{model.InstanceName}\" --proxyEnable \"False\" --proxyUsername \"\" --proxyPassword \"\" --proxyHost \"\" --proxyPort \"\"");
            expectedOutput.AppendLine($"{pathToTentacleExe} register-with --instance \"{model.InstanceName}\" --server \"{model.OctopusServerUrl}\" --name \"{model.MachineName}\" --comms-style \"TentacleActive\" --server-comms-port \"{model.ServerCommsPort}\" --apiKey \"{model.ApiKey}\" --policy \"{model.SelectedMachinePolicy}\"");
            expectedOutput.AppendLine($"{pathToTentacleExe} service --instance \"{model.InstanceName}\" --install --stop --start");
            script.Should().Be(expectedOutput.ToString().Trim());
        }
        
        [Test]
        public void WhenSettingUpPollingTentacle_AndServerRegistrationIsSkipped_ScriptShouldBeCorrectlyGenerated()
        {
            // Arrange
            var model = CreateTestSetupTentacleWizardModel();
            model.CommunicationStyle = CommunicationStyle.TentacleActive;
            model.OctopusServerUrl = "localhost";
            model.AuthMode = AuthMode.APIKey;
            model.ApiKey = "TestApiKey";
            model.MachineType = MachineType.DeploymentTarget;
            model.MachineName = "TestMachineName";
            model.SelectedSpace = "Default";
            model.HaveCredentialsBeenVerified = true;
            model.ServerCommsPort = "10943";
            model.SkipServerRegistration = true;

            // Act
            var script = model.GenerateScript().ToCommandLineString();

            // Assert
            var pathToTentacleExe = $"\"{model.PathToTentacleExe ?? CommandLine.PathToTentacleExe()}\"";
            var expectedOutput = new StringBuilder();
            expectedOutput.AppendLine($"{pathToTentacleExe} create-instance --instance \"{model.InstanceName}\" --config \"C:\\Octopus\\{model.InstanceName}\\Tentacle-{model.InstanceName}.config\"");
            expectedOutput.AppendLine($"{pathToTentacleExe} new-certificate --instance \"{model.InstanceName}\" --if-blank");
            expectedOutput.AppendLine($"{pathToTentacleExe} configure --instance \"{model.InstanceName}\" --reset-trust");
            expectedOutput.AppendLine($"{pathToTentacleExe} configure --instance \"{model.InstanceName}\" --app \"C:\\Octopus\\Applications\\{model.InstanceName}\" --port \"{model.ListenPort}\" --noListen \"True\"");
            expectedOutput.AppendLine($"{pathToTentacleExe} service --instance \"{model.InstanceName}\" --install --stop --start");
            script.Should().Be(expectedOutput.ToString().Trim());
        }
        
        [Test]
        public void WhenSettingUpPollingTentacle_ProxySettingsShouldBeDisplay()
        {
            // Arrange
            var model = CreateTestSetupTentacleWizardModel();

            // Act
            model.CommunicationStyle = CommunicationStyle.TentacleActive;

            // Assert
            model.ProxyWizardModel.ShowProxySettings.Should().BeTrue();
        }
        
        [Test]
        public void WhenSettingUpListeningTentacle_ProxySettingsShouldNotBeDisplay()
        {
            // Arrange
            var model = CreateTestSetupTentacleWizardModel();

            // Act
            model.CommunicationStyle = CommunicationStyle.TentaclePassive;

            // Assert
            model.ProxyWizardModel.ShowProxySettings.Should().BeFalse();
        }
        
        static SetupTentacleWizardModel CreateTestSetupTentacleWizardModel()
        {
            var instanceStore = Substitute.For<IApplicationInstanceStore>();
            var instanceSelectionModel = new InstanceSelectionModel(ApplicationName.Tentacle, instanceStore);
            const string tentacleInstanceName = "TestInstance";
            instanceSelectionModel.New(tentacleInstanceName);

            var model = new SetupTentacleWizardModel(instanceSelectionModel);
            
            return model;
        }
    }
}