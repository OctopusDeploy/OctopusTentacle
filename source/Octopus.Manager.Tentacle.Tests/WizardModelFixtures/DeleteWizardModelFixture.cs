using System;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Manager.Tentacle.DeleteWizard;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Manager.Tentacle.Tests.Utils;
using Octopus.Manager.Tentacle.Util;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;

namespace Octopus.Manager.Tentacle.Tests.WizardModelFixtures
{
    [TestFixture]
    public class DeleteWizardModelFixture
    {
        [Test]
        public void DeleteScriptCanBeGeneratedCorrectly()
        {
            // Arrange
            var instanceStore = Substitute.For<IApplicationInstanceStore>();
            var instanceSelectionModel = new InstanceSelectionModel(ApplicationName.Tentacle, instanceStore);
            const string tentacleInstanceName = "TestInstance";
            instanceSelectionModel.New(tentacleInstanceName);

            var model = new DeleteWizardModel(instanceSelectionModel);

            // Act
            var script = model.GenerateScript().ToCommandLineString();

            // Assert
            var pathToTentacleExe = $"\"{CommandLine.PathToTentacleExe()}\"";
            var expectedOutput = $"{pathToTentacleExe} service --instance \"{tentacleInstanceName}\" --stop --uninstall" +
                Environment.NewLine + $"{pathToTentacleExe} delete-instance --instance \"{tentacleInstanceName}\"";
            script.Should().Be(expectedOutput);
        }
    }
}