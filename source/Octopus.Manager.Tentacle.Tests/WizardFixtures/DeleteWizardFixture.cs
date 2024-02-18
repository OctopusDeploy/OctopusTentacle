using System;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Manager.Tentacle.DeleteWizard;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Manager.Tentacle.Tests.Utils;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;

namespace Octopus.Manager.Tentacle.Tests.WizardFixtures
{
    [TestFixture]
    public class DeleteWizardFixture
    {
        [Test]
        public void DeleteScriptCanBeGeneratedCorrectly()
        {
            // Arrange
            var model = CreateTestDeleteWizardModel();

            // Act
            var script = model.GenerateScript().ToCommandLineString();

            // Assert
            var pathToTentacleExe = $"\"{model.Executable}\"";
            var expectedOutput = $"{pathToTentacleExe} service --instance \"{model.InstanceName}\" --stop --uninstall" +
                Environment.NewLine + $"{pathToTentacleExe} delete-instance --instance \"{model.InstanceName}\"";
            script.Should().Be(expectedOutput);
        }
        
        static DeleteWizardModel CreateTestDeleteWizardModel()
        {
            var instanceStore = Substitute.For<IApplicationInstanceStore>();
            var instanceSelectionModel = new InstanceSelectionModel(ApplicationName.Tentacle, instanceStore);
            const string tentacleInstanceName = "TestInstance";
            instanceSelectionModel.New(tentacleInstanceName);

            var model = new DeleteWizardModel(instanceSelectionModel);
            return model;
        }
    }
}