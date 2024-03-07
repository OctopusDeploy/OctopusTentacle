using FluentAssertions;
using FluentAssertions.Execution;
using NSubstitute;
using NUnit.Framework;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Configuration.Instances;

namespace Octopus.Manager.Tentacle.Tests
{
    [TestFixture]
    public class InstanceSelectorFixture
    {
        [Test] public void WhenCreatingANewTentacleInstance()
        {
            // Arrange 
            var instanceStore = Substitute.For<IApplicationInstanceStore>();
            var instanceSelectionModel = new InstanceSelectionModel(ApplicationName.Tentacle, instanceStore);
            var instanceCount = instanceSelectionModel.Instances.Count;
            
            //Act
            const string newTentacleInstanceName = "NewInstance";
            instanceSelectionModel.New(newTentacleInstanceName);

            //Assert
            using var assertionScope = new AssertionScope();
            instanceSelectionModel.Instances.Count.Should().Be(instanceCount + 1);
            instanceSelectionModel.SelectedInstance.Should().NotBeNull();
            instanceSelectionModel.SelectedInstance.Should().Be(newTentacleInstanceName);
        }
    }
}