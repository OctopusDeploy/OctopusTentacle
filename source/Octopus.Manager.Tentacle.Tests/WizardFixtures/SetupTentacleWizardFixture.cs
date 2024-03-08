using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Client.Model;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard;
using Octopus.Manager.Tentacle.Tests.Builders;
using Octopus.Manager.Tentacle.Tests.Utils;
using Octopus.Manager.Tentacle.Util;

namespace Octopus.Manager.Tentacle.Tests.WizardFixtures
{
    public class SetupTentacleWizardFixture
    {
        [Test]
        public void WhenSettingUpListeningTentacle_ScriptShouldBeCorrectlyGenerated()
        {
            // Arrange
            var model = new SetupTentacleWizardModelBuilder().Build();
            model.CommunicationStyle = CommunicationStyle.TentaclePassive;
            model.ListenPort = "10933";
            model.OctopusThumbprint = "TestThumbprint";
            model.FirewallException = true;
            model.FirewallExceptionPossible = true;

            // Act
            var script = model.GenerateScript().ToCommandLineString();

            // Assert
            var pathToTentacleExe = $"\"{model.PathToTentacleExe ?? CommandLine.PathToTentacleExe()}\"";

            StringBuilder expectedOutput = new StringBuilder();
            expectedOutput.AppendLine($"{pathToTentacleExe} create-instance --instance \"{model.InstanceName}\" --config \"C:\\Octopus\\{model.InstanceName}\\Tentacle-{model.InstanceName}.config\"");
            expectedOutput.AppendLine($"{pathToTentacleExe} new-certificate --instance \"{model.InstanceName}\" --if-blank");
            expectedOutput.AppendLine($"{pathToTentacleExe} configure --instance \"{model.InstanceName}\" --reset-trust");
            expectedOutput.AppendLine($"{pathToTentacleExe} configure --instance \"{model.InstanceName}\" --app \"C:\\Octopus\\Applications\\{model.InstanceName}\" --port \"{model.ListenPort}\" --noListen \"False\"");
            expectedOutput.AppendLine($"{pathToTentacleExe} configure --instance \"{model.InstanceName}\" --trust \"{model.OctopusThumbprint}\"");
            expectedOutput.AppendLine($"\"netsh\" advfirewall firewall add rule \"name=Octopus Deploy Tentacle\" dir=in action=allow protocol=TCP localport={model.ListenPort}");
            expectedOutput.Append($"{pathToTentacleExe} service --instance \"{model.InstanceName}\" --install --stop --start");
            
            script.Should().Be(expectedOutput.ToString());
        }

        [Test]
        public void WhenSettingUpPollingTentacle_ScriptShouldBeCorrectlyGenerated()
        {
            // Arrange
            var model = new SetupTentacleWizardModelBuilder().Build();
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
            
            StringBuilder expectedOutput = new StringBuilder();
            expectedOutput.AppendLine($"{pathToTentacleExe} create-instance --instance \"{model.InstanceName}\" --config \"C:\\Octopus\\{model.InstanceName}\\Tentacle-{model.InstanceName}.config\"");
            expectedOutput.AppendLine($"{pathToTentacleExe} new-certificate --instance \"{model.InstanceName}\" --if-blank");
            expectedOutput.AppendLine($"{pathToTentacleExe} configure --instance \"{model.InstanceName}\" --reset-trust");
            expectedOutput.AppendLine($"{pathToTentacleExe} configure --instance \"{model.InstanceName}\" --app \"C:\\Octopus\\Applications\\{model.InstanceName}\" --port \"{model.ListenPort}\" --noListen \"True\"");
            expectedOutput.AppendLine($"{pathToTentacleExe} polling-proxy --instance \"{model.InstanceName}\" --proxyEnable \"False\" --proxyUsername \"\" --proxyPassword \"\" --proxyHost \"\" --proxyPort \"\"");
            expectedOutput.AppendLine($"{pathToTentacleExe} register-with --instance \"{model.InstanceName}\" --server \"{model.OctopusServerUrl}\" --name \"{model.MachineName}\" --comms-style \"TentacleActive\" --server-comms-port \"{model.ServerCommsPort}\" --apiKey \"{model.ApiKey}\" --policy \"{model.SelectedMachinePolicy}\"");
            expectedOutput.Append($"{pathToTentacleExe} service --instance \"{model.InstanceName}\" --install --stop --start");
            
            script.Should().Be(expectedOutput.ToString());
        }

        [Test]
        public async Task WhenSettingUpPollingTentacle_TelemetryEventShouldBeSent()
        {
            var telemetryService = new TelemetryServiceBuilder().Build();

            var model = new SetupTentacleWizardModelBuilder()
                .WithTelemetryService(telemetryService)
                .Build();

            model.OctopusServerUrl = "http://localhost";
            model.CommunicationStyle = CommunicationStyle.TentacleActive;
            model.MachineType = MachineType.DeploymentTarget;

            _ = await model.ReviewAndRunScriptTabViewModel.GenerateAndExecuteScript();

            // TODO: Replace with a less ugly assertion
            await telemetryService
                .Received(1)
                .SendTelemetryEvent(
                    new Uri("http://localhost"),
                    Arg.Is<TelemetryEvent>(t =>
                        t.EventProperties.ContainsKey("Communication Style") &&
                        t.EventProperties["Communication Style"] == CommunicationStyle.TentacleActive.ToString() &&
                        t.EventProperties.ContainsKey("Machine Type") &&
                        t.EventProperties["Machine Type"] == MachineType.DeploymentTarget.ToString()),
                    Arg.Any<IWebProxy>());
        }

        [Test]
        public async Task WhenSettingUpAListeningTentacle_TelemetryEventShouldNotBeSent()
        {
            var telemetryService = new TelemetryServiceBuilder().Build();

            var model = new SetupTentacleWizardModelBuilder()
                .WithTelemetryService(telemetryService)
                .Build();

            model.CommunicationStyle = CommunicationStyle.TentaclePassive;

            _ = await model.ReviewAndRunScriptTabViewModel.GenerateAndExecuteScript();

            await telemetryService
                .DidNotReceiveWithAnyArgs()
                .SendTelemetryEvent(null, null, null);
        }
    }
}
