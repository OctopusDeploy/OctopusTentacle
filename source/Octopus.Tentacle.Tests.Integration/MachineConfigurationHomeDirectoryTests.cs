using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Tentacle.CommonTestUtils;
using Octopus.Tentacle.Tests.Integration.Support;
using Octopus.Tentacle.Variables;

namespace Octopus.Tentacle.Tests.Integration
{
    [IntegrationTestTimeout]
    public class MachineConfigurationHomeDirectoryTests : IntegrationTest
    {
        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task ShouldUseTheDefaultMachineConfigurationHomeDirectoryWhenACustomLocationIsNotProvided(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            await using (var clientAndTentacle = await tentacleConfigurationTestCase
                             .CreateBuilder()
                             .UseDefaultMachineConfigurationHomeDirectory()
                             .Build(CancellationToken))
            {
                
                var defaultMachineConfigurationHomeDirectory = PlatformDetection.IsRunningOnWindows ? 
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Octopus", "Tentacle", "Instances") : 
                    "/etc/octopus/Tentacle/Instances";

                var expectedInstanceConfigurationFilePath = new FileInfo(Path.Combine(defaultMachineConfigurationHomeDirectory, $"{clientAndTentacle.RunningTentacle.InstanceName}.config"));

                expectedInstanceConfigurationFilePath.Exists.Should().BeTrue($"Instance configuration file should exist {expectedInstanceConfigurationFilePath.FullName}");
            }
        }

        [Test]
        [TentacleConfigurations(scriptServiceToTest: ScriptServiceVersionToTest.None)]
        public async Task ShouldUseTheCustomMachineConfigurationHomeDirectoryWhenACustomLocationIsProvided(TentacleConfigurationTestCase tentacleConfigurationTestCase)
        {
            using var tempDirectory = new TemporaryDirectory();
            await using var clientAndTentacle = await tentacleConfigurationTestCase
                .CreateBuilder()
                .UseDefaultMachineConfigurationHomeDirectory()
                .WithTentacle(x =>
                {
                    x.WithRunTentacleEnvironmentVariable(EnvironmentVariables.TentacleMachineConfigurationHomeDirectory, tempDirectory.DirectoryPath);
                })
                .Build(CancellationToken);

            var expectedInstanceConfigurationFilePath = new FileInfo(Path.Combine(tempDirectory.DirectoryPath, "Tentacle", "Instances", $"{clientAndTentacle.RunningTentacle.InstanceName}.config"));

            expectedInstanceConfigurationFilePath.Exists.Should().BeTrue($"Instance configuration file should exist {expectedInstanceConfigurationFilePath.FullName}");
        }
    }
}
