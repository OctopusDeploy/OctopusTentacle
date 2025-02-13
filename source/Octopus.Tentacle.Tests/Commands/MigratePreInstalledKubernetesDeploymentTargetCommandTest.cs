using System;
using System.Collections.Generic;
using FluentAssertions;
using k8s.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;
using Octopus.Tentacle.Commands;

namespace Octopus.Tentacle.Tests.Commands
{
    [TestFixture]
    public class MigratePreInstalledKubernetesDeploymentTargetCommandFixture
    { 
        [Test]
        public void ShouldNotMigrate_IfNoSource()
        {
            var targetConfigMap = Substitute.For<V1ConfigMap>();
            var targetSecret = Substitute.For<V1Secret>();

            var (shouldMigrate, reason) = MigratePreInstalledKubernetesDeploymentTargetCommand.ShouldMigrateData(null, null, targetConfigMap, targetSecret);
            shouldMigrate.Should().BeFalse();
            reason.Should().Be("Source config map or secret not found, skipping migration");
        }
        
        [Test]
        public void ShouldNotMigrate_IfNoDestination()
        {
            var sourceConfigMap = Substitute.For<V1ConfigMap>();
            var sourceSecret = Substitute.For<V1Secret>();
            
            MigratePreInstalledKubernetesDeploymentTargetCommand.ShouldMigrateData(sourceConfigMap, sourceSecret, null, null).Item1.Should().BeFalse();
        }
        
        [Test]
        public void ShouldNotMigrate_IfDestinationRegistered()
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