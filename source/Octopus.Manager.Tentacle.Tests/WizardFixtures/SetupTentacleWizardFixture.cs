using System;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Client.Model;
using Octopus.Manager.Tentacle.Proxy;
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
            var expectedOutput = $"{pathToTentacleExe} create-instance --instance \"{model.InstanceName}\" --config \"C:\\Octopus\\{model.InstanceName}\\Tentacle-{model.InstanceName}.config\"" +
                Environment.NewLine + $"{pathToTentacleExe} new-certificate --instance \"{model.InstanceName}\" --if-blank" +
                Environment.NewLine + $"{pathToTentacleExe} configure --instance \"{model.InstanceName}\" --reset-trust" +
                Environment.NewLine + $"{pathToTentacleExe} configure --instance \"{model.InstanceName}\" --app \"C:\\Octopus\\Applications\\{model.InstanceName}\" --port \"{model.ListenPort}\" --noListen \"True\"" +
                Environment.NewLine + $"{pathToTentacleExe} polling-proxy --instance \"{model.InstanceName}\" --proxyEnable \"False\" --proxyUsername \"\" --proxyPassword \"\" --proxyHost \"\" --proxyPort \"\"" +
                Environment.NewLine + $"{pathToTentacleExe} register-with --instance \"{model.InstanceName}\" --server \"{model.OctopusServerUrl}\" --name \"{model.MachineName}\" --comms-style \"TentacleActive\" --server-comms-port \"{model.ServerCommsPort}\" --apiKey \"{model.ApiKey}\" --policy \"{model.SelectedMachinePolicy}\"" +
                Environment.NewLine + $"{pathToTentacleExe} service --instance \"{model.InstanceName}\" --install --stop --start";
            script.Should().Be(expectedOutput);
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