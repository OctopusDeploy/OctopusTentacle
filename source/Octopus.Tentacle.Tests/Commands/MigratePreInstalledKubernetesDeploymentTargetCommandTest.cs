using System;
using System.Collections.Generic;
using FluentAssertions;
using k8s.Models;
using NSubstitute;
using NUnit.Framework;
using Octopus.Tentacle.Commands;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class MigratePreInstalledKubernetesDeploymentTargetCommandFixture
    { 
        [Test]
        public void ShouldNotMigrate_ifNoSource()
        {
            var targetConfigMap = Substitute.For<V1ConfigMap>();
            var targetSecret = Substitute.For<V1Secret>();
            
            MigratePreInstalledKubernetesDeploymentTargetCommand.ShouldMigrateData(null, null, targetConfigMap, targetSecret).Item1.Should().BeFalse();
        }
        
        [Test]
        public void ShouldNotMigrate_ifNoDestination()
        {
            var sourceConfigMap = Substitute.For<V1ConfigMap>();
            var sourceSecret = Substitute.For<V1Secret>();
            
            MigratePreInstalledKubernetesDeploymentTargetCommand.ShouldMigrateData(sourceConfigMap, sourceSecret, null, null).Item1.Should().BeFalse();
        }
        
        [Test]
        public void ShouldNotMigrate_ifDestinationRegistered()
        {
            var sourceConfigMap = Substitute.For<V1ConfigMap>();
            var sourceSecret = Substitute.For<V1Secret>();
            var targetSecret = Substitute.For<V1Secret>();
            var targetConfigMap = new V1ConfigMap
            {
                Data = new Dictionary<string, string>
                {
                    {"Tentacle.Services.IsRegistered", "true"}
                }
            };

            MigratePreInstalledKubernetesDeploymentTargetCommand.ShouldMigrateData(sourceConfigMap, sourceSecret, targetConfigMap, targetSecret).Item1.Should().BeFalse();
        }


    }
}